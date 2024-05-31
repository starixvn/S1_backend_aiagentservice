using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIAgentService.Core.Jobs;

public class BaseJob
{
    protected readonly ILogger<BaseJob> _logger;

    public BaseJob(IServiceProvider provider)
    {
        _logger = provider.GetRequiredService<ILogger<BaseJob>>();
    }

    protected string ToJson(object t)
    {
        return JsonConvert.SerializeObject(t, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }
}