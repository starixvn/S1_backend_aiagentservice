using AIAgentService.Core.Jobs.Interfaces;
using AIAgentService.Core.Models;
using Hangfire;
using Hangfire.Server;

namespace AIAgentService.Core.Jobs;

public class CommissionJob : BaseJob, IScheduledJob
{
    public CommissionJob(IServiceProvider provider) : base(provider)
    {
    }

    public Task<object> ExecuteAsync(string jobName, JobOption jobOption, PerformContext context, IJobCancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}