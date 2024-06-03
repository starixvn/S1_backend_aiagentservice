using AIAgentService.Core.Library;
using AIAgentService.Core.Models.Commission;
using AIAgentService.Core.Models.Enums;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace AIAgentService.Core.DAL;

public class CommissionService
{
    private readonly MySqlClient _mainDbClient;
    private readonly MySqlClient _reportDbClient;
    
    public CommissionService(IServiceProvider provider)
    {
        _mainDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBaseEnum.Main);
        _reportDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBaseEnum.Report);
    }
    
    public Task<IEnumerable<CommissionSettingInfo>> GetCommissionSettingList(IEnumerable<int> agentIds)
    {
        return _reportDbClient.QueryAsync<CommissionSettingInfo>("AIAgent_CommissionSetting_Get",
            parameters: new { _AgentIds = JsonConvert.SerializeObject(agentIds) });
    }
    
    public Task<int> SetCommissionSettingSnapshot(IEnumerable<int> agentIds, string yyyyMM)
    {
        return _mainDbClient.ExecuteAsync("AIAgent_BatchCommissionSettingSnapshot_Set",
                new
                {
                    _AgentIds = JsonConvert.SerializeObject(agentIds),
                    _YYYYMM = yyyyMM
                });
    }
    
    public Task<int> BatchSetCommissionLog(IEnumerable<CommissionLogInfo> infos, string yyyyMM)
    {
        return _mainDbClient.ExecuteAsync("AIAgent_BatchCommissionLog_Set",
            new
            {
                _Infos = JsonConvert.SerializeObject(infos),
                _YYYYMM = yyyyMM
            });
    }
    public Task<int> BatchSetNegativeRevenue(IEnumerable<NegativeRevenueInfo> infos, string yyyyMM)
    {
        return _mainDbClient.ExecuteAsync("AIAgent_BatchNegativeRevenue_Set",
            new
            {
                _Infos = JsonConvert.SerializeObject(infos),
                _YYYYMM = yyyyMM
            });
    }
}