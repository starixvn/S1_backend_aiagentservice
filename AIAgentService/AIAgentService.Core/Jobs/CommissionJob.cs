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

        return true;

        var option = JsonConvert.DeserializeObject<CommissionJobOption>(jobOption.Parameters);

        if (option is null)
            return true;
        
        var yyyyMM = DateTimeOffset.Now.AddMonths(-1).ToString("yyyyMM");
        
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
                    var agentList = await _agentService.GetAgentGroupList(superAgent.AgentId);
                    var agentIds = agentList.Select(x => x.AgentId);
                    
                    var commissionLogList = new List<CommissionLogInfo>();


                    foreach (var agent in agentList)
                    {
                        var custSummary = await _commissionService.GetAgentCustWinLossSummary(yyyyMM, agent.AgentId);
                        var custClaimBonus = await _commissionService.GetAgentCustClaimBonus(yyyyMM, agent.AgentId);
                        var custList = custSummary.Select(x => x.CustId)
                            .Union(custClaimBonus.Select(x => x.CustId))
                            .Distinct();
                        
                        var commissionSettings = commissionSettingList.Where(x => x.AgentId == agent.AgentId);
                       

                        foreach (var custId in custList)
                        {

                            var sg = custSummary
                                .Where(x => x.CustId == custId)
                                .GroupBy(x => x.GameType);

                            var claimBonus = custClaimBonus.FirstOrDefault(x => x.CustId == custId)?.BonusAmountClaim
                                ?? 0;

                            var partialBonus = claimBonus / sg.Count();
                            
                            /*var commissionLogs = sg.Select(x =>
                                {
                                    var commissionSetting =
                                        GetCommissionSetting(commissionSettings, agent.AgentId, x.Key);
                                    
                                    var


                                    return new CommissionLogInfo
                                    {
                                        AgentId = agent.AgentId,
                                        CustId = custId,
                                        Percentage = commissionSetting.Percentage,
                                        Commission = ,
                                        WinLossAmount = x.Sum(y => y.WinLossAmount),
                                        ClaimBonusAmount = partialBonus,
                                        GameType = x.Key
                                    };
                                });*/
                            
                            
                            
                        }

                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"CommissionJob Exception: {ex.Message}, CurrencyId:{currencyId}");

                return false; // send to retry queue?
            }
        }
        
        
        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || {jobName} completed!");
        
        return true;
    }
    
    private CommissionSettingInfo GetCommissionSetting(IEnumerable<CommissionSettingInfo> commissionSettings, int agentId, int gameType)
    {
        return commissionSettings
                .FirstOrDefault(y => y.AgentId == agentId && y.GameType == gameType)
            ?? commissionSettings
                .First(y => y.AgentId == 0 && y.GameType == gameType); // AgentId=0 is default setting
    }
    
}