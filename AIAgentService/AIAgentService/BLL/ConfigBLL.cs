using AIAgentService.Core.DAL;
using AIAgentService.Core.Library;
using AIAgentService.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;

namespace AIAgentService.Core.BLL;

public class ConfigBLL
{
    private readonly ConfigService _configService;
    private readonly ICacheHelper _redisHelper;
    private readonly IEnumerable<SiteConfig> _siteConfigs;
    private readonly ILogger<ConfigBLL> _logger;
    private readonly string _env;

    public ConfigBLL(IServiceProvider provider)
    {
        var environment = provider.GetRequiredService<IWebHostEnvironment>();
        _configService = provider.GetRequiredService<ConfigService>();
        _redisHelper = provider.GetRequiredService<ICacheHelper>();
        _siteConfigs = provider.GetServices<SiteConfig>();
        _logger = provider.GetRequiredService<ILogger<ConfigBLL>>();
        _env = environment.EnvironmentName;
    }

    public string GetConfigValue(string configKey, int siteId = 0)
    {
        var config = GetSiteConfigValue(configKey, siteId);

        if (string.IsNullOrEmpty(config) && siteId != 0)
            config = GetSiteConfigValue(configKey, 0);

        if (string.IsNullOrEmpty(config))
            _logger.LogWarning($"siteId={siteId}, configKey={configKey} not found.");

        return config;
    }

    public string GetConfigValueFromDb(string configKey, int siteId = 0, string configType = "")
    {
        var config = GetConfigFromDb(configKey, siteId, configType);

        if (string.IsNullOrEmpty(config) && siteId != 0)
            config = GetConfigFromDb(configKey, 0, configType);

        if (string.IsNullOrEmpty(config))
            _logger.LogWarning($"siteId={siteId}, configKey={configKey}, ConfigType={configType} not found.");

        return config;
    }

    public T GetConfigValue<T>(string configKey, int siteId = 0)
    {
        var json = GetConfigValue(configKey, siteId);

        return JsonConvert.DeserializeObject<T>(json);
    }

    public T GetConfigValueFromDb<T>(string configKey, int siteId = 0, string configType = "")
    {
        var json = GetConfigValueFromDb(configKey, siteId, configType);

        return JsonConvert.DeserializeObject<T>(json);
    }

    public T GetConfigValueByEnvironment<T>(string configKey, int siteId = 0)
    {
        var json = GetConfigValue($"{configKey}.{_env}.json", siteId);

        return JsonConvert.DeserializeObject<T>(json);
    }

    public T GetConfigValueFromDbByEnvironment<T>(string configKey, int siteId = 0, string configType = "")
    {
        var json = GetConfigValueFromDb($"{configKey}.{_env}.json", siteId, configType);

        return JsonConvert.DeserializeObject<T>(json);
    }

    private string GetSiteConfigValue(string configKey, int siteId)
    {
        var configs = _siteConfigs
            .FirstOrDefault(x => x.SiteId == siteId);

        if (configs == null || !configs.Config.TryGetValue(configKey, out var config))
            return string.Empty;

        return config;
    }

    private string GetConfigFromDb(string configKey, int siteId, string configType = "")
    {
        var config = GetConfigsFromDbWithCache()
            .FirstOrDefault(x => x.SiteId == siteId
                                 && x.ConfigKey.Equals(configKey)
                                 && (string.IsNullOrEmpty(configType) || x.ConfigType.Equals(configType)));

        return config == null ? string.Empty : config.ConfigValue;
    }

    private IEnumerable<Config> GetConfigsFromDbWithCache()
    {
        var dbData = _redisHelper.TryGetOrCreate("PaymentServiceConfigs", TimeSpan.FromMinutes(5),
            () => _configService.Query<Config>("SiteConfig_Get", new {ConfigGroup = "PaymentService"}));

        return dbData;
    }
}