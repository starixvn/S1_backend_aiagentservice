using AIAgentService.Core.Library;
using AIAgentService.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AIAgentService.Core.DAL;

public class ConfigService
{
    private readonly MySqlClient _reportDbClient;

    public ConfigService(IServiceProvider provider)
    {
        _reportDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBaseEnum.Report);
    }

    public IEnumerable<T> Query<T>(string sql, object? parameters = null)
    {
        return _reportDbClient.Query<T>(sql, parameters);
    }
}