using AIAgentService.Core.Library;
using AIAgentService.Core.Models.Commission;
using AIAgentService.Core.Models.Enums;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace AIAgentService.Core.DAL;

public class AgentService
{
    private readonly MySqlClient _mainDbClient;
    private readonly MySqlClient _reportDbClient;
    
    public AgentService(IServiceProvider provider)
    {
        _mainDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBaseEnum.Main);
        _reportDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBaseEnum.Report);
    }
    
    public Task<IEnumerable<AgentInfo>> GetSuperAgentList(int currencyId)
    {
        return _reportDbClient.QueryAsync<AgentInfo>("AIAgent_SuperAgentList_Get",
            parameters: new DynamicParameters(new { _CurrencyId = currencyId }));
    }
    
    public Task<IEnumerable<AgentInfo>> GetAgentGroupList(int superAgentId)
    {
        return _reportDbClient.QueryAsync<AgentInfo>("AIAgent_AgentGroup_Get",
            parameters: new DynamicParameters(new { _SuperAgentId = superAgentId }));
    }
}