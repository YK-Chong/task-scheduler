using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quartz;
using TaskScheduler.Core.DTOs;
using TaskScheduler.Core.Entities;
using TaskScheduler.Core.Interfaces;
using TaskScheduler.Infrastructure.Helper;
using TaskScheduler.Infrastructure.Jobs;

namespace TaskScheduler.Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepo;
    private readonly IExecutionHistoryRepository _historyRepo;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<TaskService> _logger;

    // Maps JobType enum to actual Quartz job class
    private static readonly Dictionary<JobType, Type> JobTypeMap = new Dictionary<JobType, Type>()
    {
        { JobType.HeartbeatJob, typeof(HeartbeatJob) },
        { JobType.ReportGenerationJob, typeof(ReportGenerationJob) },
        { JobType.SymbolDataPullJob, typeof(SymbolDataPullJob) },
        { JobType.MasterServerSyncJob, typeof(MasterServerSyncJob) }
    };

    public TaskService(
        ITaskRepository taskRepo,
        IExecutionHistoryRepository historyRepo,
        ISchedulerFactory schedulerFactory,
        ILogger<TaskService> logger)
    {
        _taskRepo = taskRepo;
        _historyRepo = historyRepo;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task<TaskResponse> CreateTaskAsync(CreateTaskRequest request)
    {
        // Validate schedule config
        if (request.ScheduleType == ScheduleType.Simple && !request.IntervalSeconds.HasValue)
            throw new ArgumentException("IntervalSeconds is required for Simple schedule type.");

        if (request.ScheduleType == ScheduleType.Cron && string.IsNullOrEmpty(request.CronExpression))
            throw new ArgumentException("CronExpression is required for Cron schedule type.");

        var task = new ScheduledTask
        {
            Name = request.Name,
            Description = request.Description,
            JobType = request.JobType,
            ScheduleType = request.ScheduleType,
            CronExpression = request.CronExpression,
            IntervalSeconds = request.IntervalSeconds,
            DisallowConcurrent = request.DisallowConcurrent,
            IsEnabled = request.IsEnabled,
            ServerId = request.ServerId,
            Metadata = request.Metadata != null
                ? JsonSerializer.Serialize(request.Metadata)
                : null
        };

        await _taskRepo.CreateAsync(task);

        // Schedule in Quartz if enabled
        if (task.IsEnabled)
            await ScheduleJobAsync(task);

        _logger.LogInformation("Task {TaskName} created with ID {TaskId}", task.Name, task.Id);

        return MapToResponse(task);
    }

    public async Task<PagedResult<TaskResponse>> GetTasksAsync(int page, int pageSize, string? jobTypeFilter, bool? isEnabledFilter)
    {
        var result = await _taskRepo.GetAllAsync(page, pageSize, jobTypeFilter, isEnabledFilter);

        return new PagedResult<TaskResponse>
        {
            Items = result.Items.Select(MapToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<TaskResponse?> GetTaskByIdAsync(string id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        return task == null ? null : MapToResponse(task);
    }

    public async Task<TaskResponse?> UpdateTaskAsync(string id, UpdateTaskRequest request)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null) return null;

        // Track if reschedule is needed
        var needsReschedule = false;

        // Update only provided fields
        if (request.Description != null) task.Description = request.Description;

        if (request.CronExpression != null)
        {
            task.CronExpression = request.CronExpression;
            needsReschedule = true;
        }

        if (request.IntervalSeconds.HasValue)
        {
            task.IntervalSeconds = request.IntervalSeconds;
            needsReschedule = true;
        }

        if (request.DisallowConcurrent.HasValue && request.DisallowConcurrent.Value != task.DisallowConcurrent)
        {
            task.DisallowConcurrent = request.DisallowConcurrent.Value;
            needsReschedule = true;
        }

        if (request.Metadata != null) task.Metadata = JsonSerializer.Serialize(request.Metadata);

        // Handle enable/disable
        if (request.IsEnabled.HasValue && request.IsEnabled.Value != task.IsEnabled)
        {
            task.IsEnabled = request.IsEnabled.Value;

            if (task.IsEnabled)
                await ScheduleJobAsync(task);
            else
                await UnscheduleJobAsync(task);
        }
        else if (task.IsEnabled && needsReschedule)
        {
            await ScheduleJobAsync(task);
        }

        await _taskRepo.UpdateAsync(task);
        return MapToResponse(task);
    }

    public async Task<bool> DeleteTaskAsync(string id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null) return false;

        await UnscheduleJobAsync(task);
        await _taskRepo.DeleteAsync(id);

        _logger.LogInformation("Task {TaskId} deleted", id);
        return true;
    }

    public async Task<TaskStatusResponse?> GetTaskStatusAsync(string id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null) return null;

        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = JobKeyHelper.GetJobKey(task);

        // Get next fire time from Quartz
        DateTime? nextTriggerAt = null;
        var triggers = await scheduler.GetTriggersOfJob(jobKey);
        var nextFireTime = triggers.Select(t => t.GetNextFireTimeUtc()).Where(t => t.HasValue).Min();
        if (nextFireTime.HasValue)
            nextTriggerAt = nextFireTime.Value.UtcDateTime;

        // Get execution history
        var histories = await _historyRepo.GetByTaskIdAsync(id);
        var latest = histories.FirstOrDefault();

        // Determine current status — default to Pending if task has never run
        var currentStatus = latest?.Status ?? ExecutionStatus.Pending;

        return new TaskStatusResponse
        {
            TaskId = task.Id,
            Name = task.Name,
            CurrentStatus = currentStatus,
            LastTriggeredAt = latest?.StartTime,
            NextTriggerAt = nextTriggerAt,
            ExecutionHistory = histories.Select(h => new ExecutionHistoryItem
            {
                ExecutionId = h.Id,
                StartTime = h.StartTime,
                EndTime = h.EndTime,
                Status = h.Status,
                DurationMs = h.DurationMs,
                ErrorMessage = h.ErrorMessage
            }).ToList()
        };
    }

    public async Task<bool> TriggerTaskAsync(string id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null) return false;

        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = JobKeyHelper.GetJobKey(task);

        if (!await scheduler.CheckExists(jobKey))
            return false;

        await scheduler.TriggerJob(jobKey);
        _logger.LogInformation("Task {TaskId} manually triggered", task.Id);
        return true;
    }

    public async Task RescheduleTaskAsync(string id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null) return;

        await ScheduleJobAsync(task);
    }

    #region Private Functions
    private async Task ScheduleJobAsync(ScheduledTask task)
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

    private async Task UnscheduleJobAsync(ScheduledTask task)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = JobKeyHelper.GetJobKey(task);

        if (await scheduler.CheckExists(jobKey))
        {
            await scheduler.DeleteJob(jobKey);
            _logger.LogInformation("Unscheduled job {JobKey}", jobKey);
        }
    }

    private static TaskResponse MapToResponse(ScheduledTask task)
    {
        return new TaskResponse
        {
            Id = task.Id,
            Name = task.Name,
            Description = task.Description,
            JobType = task.JobType,
            ScheduleType = task.ScheduleType,
            CronExpression = task.CronExpression,
            IntervalSeconds = task.IntervalSeconds,
            DisallowConcurrent = task.DisallowConcurrent,
            IsEnabled = task.IsEnabled,
            ServerId = task.ServerId,
            Metadata = task.Metadata != null
                ? JsonSerializer.Deserialize<object>(task.Metadata)
                : null,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }
    #endregion
}