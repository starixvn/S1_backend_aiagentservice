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
    
    public Task<IEnumerable<CommissionSettingInfo>> GetCommissionSettingList()
    {
        return _reportDbClient.QueryAsync<CommissionSettingInfo>("AIAgent_CommissionSetting_Get");
    }
    
    public Task<int> SetCommissionSettingSnapshot(string yyyyMM)
    {
        return _mainDbClient.ExecuteAsync("AIAgent_BatchCommissionSettingSnapshot_Set",
                new
                {
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
    
    public Task<IEnumerable<NegativeRevenueInfo>> GetNegativeRevenueList(string yyyyMM, IEnumerable<int> agentIds)
    {
        return _reportDbClient.QueryAsync<NegativeRevenueInfo>("AIAgent_NegativeRevenueList_Get",
            parameters: new
            {
                _YYYYMM = yyyyMM,
                _AgentIds = JsonConvert.SerializeObject(agentIds)
            });
    }
    
    public Task<IEnumerable<CustWinLossSummaryInfo>> GetAgentCustWinLossSummary(string yyyyMM, int agentId)
    {
        return _reportDbClient.QueryAsync<CustWinLossSummaryInfo>("AIAgent_AgentCustWinLossSummary_Get",
            parameters: new
            {
                _YYYYMM = yyyyMM,
                _AgentId = agentId
            });
    }
    
    public Task<IEnumerable<CustClaimBonusInfo>> GetAgentCustClaimBonus(string yyyyMM, int agentId)
    {
        return _reportDbClient.QueryAsync<CustClaimBonusInfo>("AIAgent_AgentCustClaimBonus_Get",
            parameters: new
            {
                _YYYYMM = yyyyMM,
                _AgentId = agentId
            });
    }
}