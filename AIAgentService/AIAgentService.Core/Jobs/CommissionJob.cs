using AIAgentService.Core.BLL;
using AIAgentService.Core.DAL;
using AIAgentService.Core.Exceptions;
using AIAgentService.Core.Jobs.Interfaces;
using AIAgentService.Core.Library;
using AIAgentService.Core.Models;
using AIAgentService.Core.Models.Commission;
using AIAgentService.Core.Models.Enums;
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
    private readonly MySqlClient _mainDbClient;
    private readonly CommissionBll _commissionBll;
    
    public CommissionJob(IServiceProvider provider) : base(provider)
    {
        _commissionService = provider.GetRequiredService<CommissionService>();
        _agentService = provider.GetRequiredService<AgentService>();
        _mainDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBaseEnum.Main);
        
        _commissionBll = provider.GetRequiredService<CommissionBll>();
        
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
        
        await using var conn = _mainDbClient.GetMySqlConnection();
        await conn.OpenAsync();
        
        foreach (var currencyId in option.CurrencyIds)
        {
            try
            {
                var superAgentList = await _agentService.GetSuperAgentList(currencyId);
                
                foreach (var superAgent in superAgentList)
                {
                    
                    await using var trans = await conn.BeginTransactionAsync();
                    var calculateSuccess= await _commissionBll.CalculateCommission(superAgent.AgentId, yyyyMM, commissionSettingList, trans);
                    
                    if (!calculateSuccess)
                    {
                        
                    }
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
        await conn.CloseAsync();
        
        return true;
    }
}