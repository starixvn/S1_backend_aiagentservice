using System.Data;
using AIAgentService.Core.DAL;
using AIAgentService.Core.Exceptions;
using AIAgentService.Core.Models.Commission;
using AIAgentService.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIAgentService.Core.BLL;

public class CommissionBll
{
    private readonly AgentService _agentService;
    private readonly CommissionService _commissionService;
    private readonly ILogger<CommissionBll> _logger;
    
    public CommissionBll(IServiceProvider provider)
    {
        _commissionService = provider.GetRequiredService<CommissionService>();
        _agentService = provider.GetRequiredService<AgentService>();
        _logger = provider.GetRequiredService<ILogger<CommissionBll>>();
    }

    public async Task<bool> CalculateCommission(int superAgentId, string yyyyMM, IEnumerable<CommissionSettingInfo> commissionSettingList, IDbTransaction trans)
    {
        try
        {
            var agentList = (await _agentService.GetAgentGroupList(superAgentId))
                .OrderByDescending(x => x.Level)
                .ToList();
            
            var agentIds = agentList.Select(x => x.AgentId);
            var lastMonth = DateTimeOffset.Parse($"{yyyyMM}01").AddMonths(-1);
            var lastMonthNegativeRevenueList =
                await _commissionService.GetNegativeRevenueList(lastMonth.ToString("yyyyMM"), agentIds);
            
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
                        var commissionSetting = GetCommissionSetting(commissionSettings, agent.AgentId, gameType);
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

            if (commissionLogList.Any())
            {
                var result = await _commissionService.BatchSetCommissionLog(commissionLogList, yyyyMM, trans);

                if (result <= 0)
                    throw new AIAgentException(ErrorCodeEnum.ExecuteFailRollback, "Execute BatchSetCommissionLog failed.");
            }

            if (negativeRevenueList.Any())
            {
                var result = await _commissionService.BatchSetNegativeRevenue(negativeRevenueList, yyyyMM, trans);

                if (result <= 0)
                    throw new AIAgentException(ErrorCodeEnum.ExecuteFailRollback, "Execute BatchSetNegativeRevenue failed.");
            }
            
            trans.Commit();
            return true;
        }
        catch (AIAgentException ex)
        {
            _logger.LogInformation($"{ex.Message}, superAgentId={superAgentId}, yyyyMM={yyyyMM}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"CalculateCommission Exception, superAgentId={superAgentId}, yyyyMM={yyyyMM}");
        }

        trans.Rollback();
        return false;
    }
    
    private static CommissionSettingInfo GetCommissionSetting(in IEnumerable<CommissionSettingInfo> commissionSettings, int agentId, int gameType)
    {
        return commissionSettings
                   .FirstOrDefault(y => y.AgentId == agentId && y.GameType == gameType)
               ?? commissionSettings
                   .First(y => y.AgentId == 0 && y.GameType == gameType); // AgentId=0 is default setting
    }
    
    private static void SetSharing(AgentInfo currentAgent,
        decimal commission,
        int gameType,
        in IEnumerable<AgentInfo> agentInfos,
        in IEnumerable<CommissionSettingInfo> commissionSettings,
        ref List<CommissionLogInfo> commissionLogInfos)
    {
        while (currentAgent.ParentAgentId != 0)
        {
            var parentAgent = agentInfos.First(x => x.AgentId == currentAgent.ParentAgentId);
            var commissionSetting = GetCommissionSetting(commissionSettings, currentAgent.AgentId, gameType);
            
            commissionLogInfos.Add(new CommissionLogInfo
            {
                AgentId = currentAgent.ParentAgentId,
                GameType = gameType,
                Sharing = commission * commissionSetting.UpperPercentage
            });
            
            currentAgent = parentAgent;
        };
    }
}