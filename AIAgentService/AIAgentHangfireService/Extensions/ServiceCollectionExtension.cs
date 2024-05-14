using AIAgentService.Core.Library;
using AIAgentService.Core.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AIAgenHangfireService.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddDatabaseConnection(this IServiceCollection services, IConfiguration config)
    {
        var serviceType = Environment.GetEnvironmentVariable("ServiceType") ?? config.GetValue<string>("ServiceType");
        var reportDBConn = config.GetConnectionString($"{serviceType}_ReportDB");
        var reportBackupDB1 = config.GetConnectionString($"{serviceType}_ReportBackupDB1");
        var reportBackupDB2 = config.GetConnectionString($"{serviceType}_ReportBackupDB2");

        //檢查是否有BackupDB連線字串
        if (!string.IsNullOrEmpty(reportBackupDB1) && !string.IsNullOrEmpty(reportBackupDB2))
        {
            // 設定ReportDB連線順序
            services.AddSingleton(provider => new MySqlClient(provider, DataBaseEnum.Report, reportDBConn,
                new List<string>()
                {
                    reportDBConn, //1b
                    reportBackupDB1, //1c
                    reportBackupDB2, //1a
                }));

            // 設定ReportBackupDB連線順序
            services.AddSingleton(provider => new MySqlClient(provider, DataBaseEnum.ReportBackupDB, reportBackupDB1,
                new List<string>()
                {
                    reportBackupDB1, //1c
                    reportDBConn, //1b
                    reportBackupDB2, //1a
                }));
        }
        else
        {
            services.AddSingleton(provider => new MySqlClient(provider, DataBaseEnum.Report, reportDBConn));
        }

        services.AddSingleton(provider =>
            new MySqlClient(provider, DataBaseEnum.Main, config.GetConnectionString($"{serviceType}_MainDB")));
        
        return services;
    }

    public static IServiceCollection AddRedisConnection(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(configuration.GetValue<string>("RedisConnection")))
            .AddSingleton<ICacheHelper, RedisCacheHelper>();
    }
}