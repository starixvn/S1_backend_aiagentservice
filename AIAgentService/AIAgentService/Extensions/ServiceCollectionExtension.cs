using AIAgentService.Core.BLL;
using AIAgentService.Core.DAL;
using AIAgentService.Core.Library;
using AIAgentService.Core.Models;
using AIAgentService.Core.Models.Enums;
using AIAgentService.Core.Models.Options;
using AIAgentService.Exceptions;
using StackExchange.Redis;

namespace AIAgentService.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddDatabaseConnection(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceType = configuration.GetValue<string>("ServiceType");

        var reportDbConn = configuration.GetConnectionString($"{serviceType}_ReportDB");
        var reportBackupDb1 = configuration.GetConnectionString($"{serviceType}_ReportBackupDB1");

        //檢查是否有BackupDB連線字串
        if (!string.IsNullOrEmpty(reportBackupDb1))
        {
            // 設定ReportDB連線順序，AIAgentService優先使用1c，備援1b
            services.AddSingleton(provider => new MySqlClient(provider, DataBaseEnum.Report, reportBackupDb1, new List<string>
            {                
                reportBackupDb1,
                reportDbConn
            }));
        }
        else
        {
            services.AddSingleton(provider => new MySqlClient(provider, DataBaseEnum.Report, reportDbConn));
        }
        
        return services;
    }
    
    public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = ConnectionMultiplexer.Connect(configuration.GetValue<string>("redisConnection"));
        services.AddSingleton<IConnectionMultiplexer>(redisConnection);
        
        // DAL
        var dalAssemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var item in dalAssemblies)
        {
            var list = item.DefinedTypes.Where(t => t.IsClass
                                                    && t.Namespace == "AIAgentService.Core.DAL"
                                                    && t.Name.Contains("Service")).ToList();

            list.ForEach((type) =>
            {
                services.AddSingleton(type, type);
            });
        }

        // BLL
        var bllAssemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var item in bllAssemblies)
        {
            var list = item.DefinedTypes.Where(t => t.IsClass
                                                    && t.Namespace == "AIAgentService.Service.BLL"
                                                    && t.Name.Contains("BLL")).ToList();

            list.ForEach((type) =>
            {
                services.AddSingleton(type, type);
            });
        }
        
        services.AddOptionListByDb<ServerInfo>("ServerInfoList_Get");
        services.AddOptionListByDb<CurrencyOption>("Common_Currency_Get");
        services.AddOptionListByDb<Config>("SiteConfig_Get", new { ConfigGroup = "AIAgentService" });
        services.AddSiteConfig();
        services.AddSingleton<ConfigBLL>();
        
        // Filters
        services.AddMvc(config =>
        {
            config.Filters.Add<ExceptionFilter>();
        });
        
        return services
            .AddSingleton<ICacheHelper, RedisCacheHelper>();
    }
    
    private static IServiceCollection AddOptionListByDb<T>(this IServiceCollection services, string sql, object parameters = null)
    {
        var provider = services.BuildServiceProvider();
        var configService = provider.GetRequiredService<ConfigService>();
        var option = configService.Query<T>(sql, parameters);
        services.AddSingleton(typeof(IEnumerable<T>), p => option);
        
        return services;
    }
    
    private static IServiceCollection AddSiteConfig(this IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        // 準備ServerInfo
        var serverInfoList = new List<ServerInfo> { new() { SiteId = 0, SiteName = "default" } };
        serverInfoList.AddRange(provider.GetRequiredService<IEnumerable<ServerInfo>>());

        // 準備Config
        var siteConfigGroup = provider.GetRequiredService<IEnumerable<Config>>().GroupBy(i => i.SiteId);

        foreach (var serverInfo in serverInfoList)
        {
            //設定SiteConfig
            var siteConfig = siteConfigGroup
                .FirstOrDefault(i => i.Key == serverInfo.SiteId) ?? siteConfigGroup.First(i => i.Key == 0);
            var config = siteConfig.ToDictionary(i => i.ConfigKey, i => i.ConfigValue);
            services.AddSingleton(new SiteConfig(serverInfo, config));
        }

        return services;
    }
}