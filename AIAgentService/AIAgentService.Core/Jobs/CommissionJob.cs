using AIAgentService.Core.DAL;
using AIAgentService.Core.Jobs.Interfaces;
using AIAgentService.Core.Models;
using AIAgentService.Core.Models.Commission;
using AIAgentService.Core.Models.Options;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIAgentService.Core.Jobs;

public class CommissionJob : BaseJob, IScheduledJob
{
    private readonly CommissionService _commissionService;
    private readonly AgentService _agentService;
    
    public CommissionJob(IServiceProvider provider) : base(provider)
    {
        _commissionService = provider.GetRequiredService<CommissionService>();
        _agentService = provider.GetRequiredService<AgentService>();
    }

    public async Task<object> ExecuteAsync(string jobName, JobOption jobOption, PerformContext context, IJobCancellationToken cancellationToken)
    {
        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || {jobName} running......");

        var option = JsonConvert.DeserializeObject<CommissionJobOption>(jobOption.Parameters);

        if (option is null)
            return true;
        var now = DateTimeOffset.Now;
        var yyyyMM = now.AddMonths(-1).ToString("yyyyMM");
        
        var commissionSettingList = await _commissionService.GetCommissionSettingList();
        var result = await _commissionService.SetCommissionSettingSnapshot(yyyyMM);

        if (result < 1)
        {
            _logger.LogInformation($"CommissionJob SetCommissionSettingSnapshot Failed.");

            return false;
        }
        
        foreach (var currencyId in option.CurrencyIds)
        {
            try
            {
                var superAgentList = await _agentService.GetSuperAgentList(currencyId);
                
                foreach (var superAgent in superAgentList)
                {
                    var agentList = (await _agentService.GetAgentGroupList(superAgent.AgentId))
                        .OrderByDescending(x => x.Level)
                        .ToList();
                    
                    var agentIds = agentList.Select(x => x.AgentId);
                    var lastMonthNegativeRevenueList =
                        await _commissionService.GetNegativeRevenueList(now.AddMonths(-2).ToString("yyyyMM"), agentIds);
                    
                    var commissionLogList = new List<CommissionLogInfo>();
                    var negativeRevenueList = new List<NegativeRevenueInfo>();

                    foreach (var agent in agentList)
                    {
                        var agentId = agent.AgentId;
                        var custSummary = await _commissionService.GetAgentCustWinLossSummary(yyyyMM, agentId);
                        var custClaimBonus = await _commissionService.GetAgentCustClaimBonus(yyyyMM, agentId);
                        
                        var custList = custSummary.Select(x => x.CustId)
                            .Union(custClaimBonus.Select(x => x.CustId))
                            .Distinct();
                        
                        var commissionSettings = commissionSettingList
                            .Where(x => x.AgentId == agentId || x.AgentId == 0);

                        var negativeRevenue = 0m;
                        var lastMonthNegativeRevenue = lastMonthNegativeRevenueList
                            .FirstOrDefault(x => x.AgentId == agentId)?.NegativeRevenue ?? 0;
                        
                        // Log加入前月負營利
                        if (lastMonthNegativeRevenue < 0)
                        {
                            commissionLogList.Add(new CommissionLogInfo
                            {
                                AgentId = agentId,
                                LastMonthNegativeRevenue = lastMonthNegativeRevenue
                            });
                        }
                        
                        foreach (var custId in custList)
                        {
                            var summaryGroup = custSummary
                                .Where(x => x.CustId == custId)
                                .GroupBy(x => x.GameType);

                            var claimBonus = custClaimBonus
                                .FirstOrDefault(x => x.CustId == custId)?.BonusAmountClaim ?? 0;
                            
                            // GGR計算為WinLossAmount*-1 - ClaimBonusAmount，但Percentage是分GameType設定，因此把ClaimBonusAmount平分給每種GameType
                            var partialBonus = decimal.Round(claimBonus / summaryGroup.Count(), 2);
                         
                            // Log加入用戶輸贏
                            foreach (var group in summaryGroup)
                            {
                                var gameType = group.Key;
                                var commissionSetting =
                                    GetCommissionSetting(commissionSettings, agent.AgentId, gameType);
                                var winloss = group.Sum(y => y.WinLossAmount);
                                var tmpCommission = (winloss * -1 - partialBonus) * commissionSetting.Percentage;

                                commissionLogList.Add(new CommissionLogInfo
                                {
                                    AgentId = agent.AgentId,
                                    CustId = custId,
                                    Percentage = commissionSetting.Percentage,
                                    ClaimBonusAmount = partialBonus,
                                    WinLossAmount = winloss,
                                    Commission = tmpCommission > 0 ? tmpCommission : 0,
                                    GameType = gameType
                                });
                                
                                
                                // 負營利
                                if (tmpCommission < 0)
                                {
                                    negativeRevenue += tmpCommission;
                                    continue;
                                }
                                    
                                // 計算上層分潤
                                SetSharing(agent, tmpCommission, gameType, agentList, commissionSettings,
                                    ref commissionLogList);
                            }
                        }
                        
                        if (negativeRevenue < 0)
                            negativeRevenueList.Add(new NegativeRevenueInfo
                            {
                                AgentId = agentId,
                                NegativeRevenue = negativeRevenue
                            });
                    }

                    // todo: 包交易
                    result = await _commissionService.BatchSetCommissionLog(commissionLogList, yyyyMM);
                    result = await _commissionService.BatchSetNegativeRevenue(negativeRevenueList, yyyyMM);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"CommissionJob Exception: {ex.Message}, CurrencyId:{currencyId}");
                
                // todo: rollback & send to retry queue
                return false;
            }
        }
        
        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || {jobName} completed!");
        
        return true;
    }
    
    private CommissionSettingInfo GetCommissionSetting(in IEnumerable<CommissionSettingInfo> commissionSettings, int agentId, int gameType)
    {
        return commissionSettings
                .FirstOrDefault(y => y.AgentId == agentId && y.GameType == gameType)
            ?? commissionSettings
                .First(y => y.AgentId == 0 && y.GameType == gameType); // AgentId=0 is default setting
    }
    
    private void SetSharing(AgentInfo currentAgentInfo,
        decimal commission,
        int gameType,
        in IEnumerable<AgentInfo> agentInfos,
        in IEnumerable<CommissionSettingInfo> commissionSettings,
        ref List<CommissionLogInfo> commissionLogInfos)
    {
        var currentAgentParentId = currentAgentInfo.ParentAgentId;
        if (currentAgentParentId == 0)
            return;
        
        do
        {
            var parentAgentInfo = agentInfos.First(x => x.AgentId == currentAgentParentId);
            var commissionSetting = GetCommissionSetting(commissionSettings, currentAgentInfo.AgentId, gameType);
            
            commissionLogInfos.Add(new CommissionLogInfo
            {
                AgentId = currentAgentParentId,
                GameType = gameType,
                Sharing = commission * commissionSetting.UpperPercentage
            });
            
            currentAgentParentId = parentAgentInfo.ParentAgentId;
            
        } while (currentAgentParentId != 0);
    }
}