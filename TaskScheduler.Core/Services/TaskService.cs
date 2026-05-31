using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaskScheduler.Core.DTOs;
using TaskScheduler.Core.Entities;
using TaskScheduler.Core.Interfaces;

namespace TaskScheduler.Core.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepo;
    private readonly IExecutionHistoryRepository _historyRepo;
    private readonly ITaskScheduler _taskScheduler;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        ITaskRepository taskRepo,
        IExecutionHistoryRepository historyRepo,
        ITaskScheduler taskScheduler,
        ILogger<TaskService> logger)
    {
        _taskRepo = taskRepo;
        _historyRepo = historyRepo;
        _taskScheduler = taskScheduler;
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
            await _taskScheduler.ScheduleAsync(task);

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
                await _taskScheduler.ScheduleAsync(task);
            else
                await _taskScheduler.UnscheduleAsync(task);
        }
        else if (task.IsEnabled && needsReschedule)
        {
            await _taskScheduler.ScheduleAsync(task);
        }

        await _taskRepo.UpdateAsync(task);
        return MapToResponse(task);
    }

    public async Task<bool> DeleteTaskAsync(string id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null) return false;

        await _taskScheduler.UnscheduleAsync(task);
        await _taskRepo.DeleteAsync(id);

        _logger.LogInformation("Task {TaskId} deleted", id);
        return true;
    }

    public async Task<TaskStatusResponse?> GetTaskStatusAsync(string id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null) return null;

        var nextTriggerAt = await _taskScheduler.GetNextFireTimeAsync(task);

        // Get execution history
        var histories = await _historyRepo.GetByTaskIdAsync(id);
        var latest = histories.FirstOrDefault();

        return new TaskStatusResponse
        {
            TaskId = task.Id,
            Name = task.Name,
            CurrentStatus = latest?.Status ?? ExecutionStatus.Pending,
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

        var triggered = await _taskScheduler.TriggerAsync(task);
        if (triggered)
            _logger.LogInformation("Task {TaskId} manually triggered", task.Id);

        return triggered;
    }

    public async Task RescheduleTaskAsync(string id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null) return;

        await _taskScheduler.ScheduleAsync(task);
    }

    #region Private Functions
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
