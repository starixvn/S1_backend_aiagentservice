using AIAgentService.Core.DAL;
using AIAgentService.Core.Jobs;
using AIAgentService.Core.Jobs.Interfaces;
using AIAgentService.Core.Library;
using AIAgentService.Core.Models;
using AIAgentService.Core.Models.Enums;
using AIAgentService.Core.Models.Options;
using Hangfire;
using Hangfire.Console.Extensions;
using Hangfire.Dashboard.Management;
using Hangfire.MemoryStorage;
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
    
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<ConfigService>();
    }
    
    public static IServiceCollection RegisterScheduledJobs(this IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        var configService = provider.GetRequiredService<ConfigService>();

        // 取得啟用的Job
        var enableJobList = configService.Query<JobOption>("Service_ScheduledJob_Get", new { Project = "Hangfire" })
            .Where(r => r.Enable);

        // 依照Job的Group進行分類整理並設定到MyJobManager
        var jobGroups = enableJobList.GroupBy(i => i.Group).ToDictionary(i => i.Key, i => i.ToList());
        services.AddSingleton(serviceProvider => jobGroups);
        MyJobManager.SettingJob(jobGroups);

        // 取得有實作IScheduledJob的Class列表
        var scheduledJobs = typeof(IScheduledJob).Assembly.GetTypes().Where(item => item.GetInterfaces().Contains(typeof(IScheduledJob)));

        // 依照啟用的Job建立Instance
        foreach (var jobOption in enableJobList)
        {
            var job = scheduledJobs.FirstOrDefault(i => string.Equals(i.Name, jobOption.ClassName, StringComparison.OrdinalIgnoreCase));
            if (job != null)
            {
                services.AddSingleton(serviceProvider => (IScheduledJob)Activator.CreateInstance(job, new object[] { serviceProvider }));
            }
        }

        return services;
    }
    
    public static IServiceCollection AddHangfireService(this IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        var jobGroups = provider.GetRequiredService<Dictionary<string, List<JobOption>>>();

        return services
        .AddHangfire(config =>
            {
                config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseColouredConsoleLogProvider()
                .UseDashboardMetric(Hangfire.Dashboard.DashboardMetrics.RecurringJobCount)
                .UseDashboardMetric(Hangfire.Dashboard.DashboardMetrics.RetriesCount)
                .UseDashboardMetric(Hangfire.Dashboard.DashboardMetrics.ProcessingCount)
                .UseDashboardMetric(Hangfire.Dashboard.DashboardMetrics.SucceededCount)
                .UseDashboardMetric(Hangfire.Dashboard.DashboardMetrics.FailedCount)
                .UseDashboardMetric(Hangfire.Dashboard.DashboardMetrics.AwaitingCount)
                .UseManagementPages(p => p.AddJobs(()
                    => typeof(IScheduledJob).Assembly.GetTypes().Where(item => item.GetInterfaces().Contains(typeof(IScheduledJob))).ToArray()))
                .UseSerilogLogProvider()
                .UseMemoryStorage();
            })
        .AddHangfireConsoleExtensions()
        .AddHangfireServer(config =>
        {
            config.SchedulePollingInterval = TimeSpan.FromSeconds(15);
            var queues = jobGroups.Select(item => item.Key.ToLower()).ToList();
            if (!queues.Contains("default"))
            {
                queues.Add("default");
            }
            config.Queues = queues.ToArray();
            config.WorkerCount = Environment.ProcessorCount * 10;
        });
    }
    
    private static IServiceCollection AddOptionListByDB<T>(this IServiceCollection services, string sql, object parameters = null)
    {
        var provider = services.BuildServiceProvider();
        var configService = provider.GetRequiredService<ConfigService>();
        var option = configService.Query<T>(sql, parameters);
        services.AddSingleton(typeof(IEnumerable<T>), p => option);
        return services;
    }

    
    public static IServiceCollection AddOption(this IServiceCollection services)
    {
        return services.AddOptionListByDB<CurrencyOption>("Common_Currency_Get");
    }

    public static IServiceCollection AddConfig(this IServiceCollection services)
    {
        return services.AddOptionListByDB<Config>("SiteConfig_Get", new { ConfigGroup = "HangfireService" });
    }

    public static IServiceCollection AddServerInfo(this IServiceCollection services)
    {
        return services.AddOptionListByDB<ServerInfo>("Admin_ServerInfoList_Get");
    }
    
    
    public static IServiceCollection AddSiteConfig(this IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        // 準備ServerInfo
        var serverInfoList = new List<ServerInfo>() { new ServerInfo() { SiteId = 0, SiteName = "default" } };
        serverInfoList.AddRange(provider.GetRequiredService<IEnumerable<ServerInfo>>());

        // 準備Config
        var siteConfigGroup = provider.GetRequiredService<IEnumerable<Config>>().GroupBy(i => i.SiteId);

        foreach (var serverInfo in serverInfoList)
        {
            //設定SiteConfig
            var siteConfig = siteConfigGroup.FirstOrDefault(i => i.Key == serverInfo.SiteId) ?? siteConfigGroup.First(i => i.Key == 0);
            var config = siteConfig.ToDictionary(i => i.ConfigKey, i => i.ConfigValue);
            services.AddSingleton(new SiteConfig(serverInfo, config));
        }
        return services;
    }
}