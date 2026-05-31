using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quartz;
using TaskScheduler.Core.Entities;
using TaskScheduler.Core.Interfaces;
using TaskScheduler.Infrastructure.Helper;
using TaskScheduler.Infrastructure.Jobs;

namespace TaskScheduler.Infrastructure.Scheduler;

public class QuartzTaskScheduler : ITaskScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<QuartzTaskScheduler> _logger;

    // Maps JobType enum to actual Quartz job class
    private static readonly Dictionary<JobType, Type> JobTypeMap = new()
    {
        { JobType.HeartbeatJob, typeof(HeartbeatJob) },
        { JobType.ReportGenerationJob, typeof(ReportGenerationJob) },
        { JobType.SymbolDataPullJob, typeof(SymbolDataPullJob) },
        { JobType.MasterServerSyncJob, typeof(MasterServerSyncJob) }
    };

    public QuartzTaskScheduler(ISchedulerFactory schedulerFactory, ILogger<QuartzTaskScheduler> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task ScheduleAsync(ScheduledTask task)
    {
        if (!JobTypeMap.TryGetValue(task.JobType, out var jobClass))
        {
            _logger.LogWarning("No job class mapped for JobType {JobType}", task.JobType);
            return;
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = JobKeyHelper.GetJobKey(task);

        // Build job with metadata from task
        var jobBuilder = JobBuilder.Create(jobClass)
            .WithIdentity(jobKey)
            .StoreDurably()
            .UsingJobData("taskId", task.Id)
            .UsingJobData("taskName", task.Name);

        if (!string.IsNullOrEmpty(task.ServerId))
            jobBuilder.UsingJobData("serverId", task.ServerId);

        // Concurrency control based on flag
        if (task.DisallowConcurrent)
            jobBuilder = jobBuilder.DisallowConcurrentExecution();

        // Pass metadata into JobDataMap so jobs can read config
        if (!string.IsNullOrEmpty(task.Metadata))
        {
            var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(task.Metadata);
            if (metadata != null)
                foreach (var kvp in metadata)
                    jobBuilder.UsingJobData(kvp.Key, kvp.Value.ToString());
        }

        var job = jobBuilder.Build();

        // Quartz throws ObjectAlreadyExistsException if job key exists — delete first to allow reschedule
        if (await scheduler.CheckExists(jobKey))
            await scheduler.DeleteJob(jobKey);

        // Build trigger based on schedule type
        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity(JobKeyHelper.GetTriggerKey(task))
            .StartNow();

        if (task.ScheduleType == ScheduleType.Simple)
        {
            triggerBuilder.WithSimpleSchedule(x => x
                .WithIntervalInSeconds(task.IntervalSeconds!.Value)
                .RepeatForever()
                .WithMisfireHandlingInstructionNextWithRemainingCount());
        }
        else
        {
            triggerBuilder.WithCronSchedule(task.CronExpression!);
        }

        var trigger = triggerBuilder.Build();

        await scheduler.ScheduleJob(job, trigger);
        _logger.LogInformation("Scheduled job {JobKey}", jobKey);
    }

    public async Task UnscheduleAsync(ScheduledTask task)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = JobKeyHelper.GetJobKey(task);

        if (await scheduler.CheckExists(jobKey))
        {
            await scheduler.DeleteJob(jobKey);
            _logger.LogInformation("Unscheduled job {JobKey}", jobKey);
        }
    }

    public async Task<bool> TriggerAsync(ScheduledTask task)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = JobKeyHelper.GetJobKey(task);

        if (!await scheduler.CheckExists(jobKey))
            return false;

        await scheduler.TriggerJob(jobKey);
        return true;
    }

    public async Task<bool> CheckExistsAsync(ScheduledTask task)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        return await scheduler.CheckExists(JobKeyHelper.GetJobKey(task));
    }

    public async Task<DateTime?> GetNextFireTimeAsync(ScheduledTask task)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var triggers = await scheduler.GetTriggersOfJob(JobKeyHelper.GetJobKey(task));
        var nextFireTime = triggers.Select(t => t.GetNextFireTimeUtc()).Where(t => t.HasValue).Min();
        return nextFireTime?.UtcDateTime;
    }
}
