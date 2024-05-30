using System.ComponentModel;
using AIAgentService.Core.Jobs.Interfaces;
using AIAgentService.Core.Models;
using Hangfire;
using Hangfire.Dashboard.Management.Pages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AIAgentService.Core.Jobs;

public static class MyJobManager
{
    private static IReadOnlyDictionary<string, List<JobOption>> _jobGroups;

    public static void SettingJob(Dictionary<string, List<JobOption>> jobGroups)
    {
        _jobGroups = jobGroups;
    }

    public static IReadOnlyDictionary<string, List<JobOption>> GetJobGroups()
    {
        return _jobGroups;
    }

    public static void AddRecurringJob(IServiceProvider provider)
    {
        var webHostEnvironment = provider.GetRequiredService<IWebHostEnvironment>();
        var jobManager = new RecurringJobManager();

        foreach (var jobGroup in _jobGroups)
        {
            var queue = jobGroup.Key.ToLower();
            var mins = 0;
            foreach (var option in jobGroup.Value)
            {
                if (option.Enable)
                {
                    var job = provider.GetServices<IScheduledJob>()
                        .FirstOrDefault(job => job.GetType().Name.Equals(option.ClassName, StringComparison.OrdinalIgnoreCase));

                    if (job != null)
                    {
                        jobManager.AddOrUpdate(
                          recurringJobId: option.JobName,
                          methodCall: () => job.ExecuteAsync(option.JobName, option, null, null),
                          cronExpression: option.Cron,
                          queue: queue,
                          timeZone: TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"));

                        //都先設定為暫停狀態
                        Pause(option.JobName);
                    }
                }
            }
        }

        //全部job延遲時啟動    
        if (!webHostEnvironment.EnvironmentName.Equals("Local"))
        {
            AllDelayStart();
        }
    }

    public static void Pause(string jobName)
    {
        using var connection = JobStorage.Current.GetConnection();
        var job = connection.GetRecurringJob(jobName);
        if (job != null && !job.PauseState)
        {
            connection.SetPauseState(jobName, true);
        }
    }

    public static void Start(string jobName)
    {
        using var connection = JobStorage.Current.GetConnection();
        var job = connection.GetRecurringJob(jobName);
        if (job != null && job.PauseState)
        {
            connection.SetPauseState(jobName, false);
        }
    }

    [DisplayName("{0} restart after {1} hours, because {2}.")]
    public static void RestartByHours(string jobName, int pauseHours, string reason)
    {
        using var connection = JobStorage.Current.GetConnection();
        if (connection.GetRecurringJob(jobName).PauseState)
        {
            connection.SetPauseState(jobName, false);
        }
    }

    public static void AllDelayStart()
    {
        using var connection = JobStorage.Current.GetConnection();
        var seconds = 0;
        foreach (var job in connection.GetRecurringJobs())
        {
            BackgroundJob.Schedule(() => StartAfterSeconds(job.Id, seconds), TimeSpan.FromSeconds(seconds));
            seconds += 5;
        }
    }

    [DisplayName("{0} start after {1} Seconds")]
    public static void StartAfterSeconds(string jobName, int Seconds)
    {
        using var connection = JobStorage.Current.GetConnection();
        if (connection.GetRecurringJob(jobName).PauseState)
        {
            connection.SetPauseState(jobName, false);
        }
    }
}