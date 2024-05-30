using AIAgentService.Core.Models;
using Hangfire;
using Hangfire.Server;

namespace AIAgentService.Core.Jobs.Interfaces;

public interface IScheduledJob
{
    Task<object> ExecuteAsync(string jobName, JobOption jobOption, PerformContext context, IJobCancellationToken cancellationToken);
}