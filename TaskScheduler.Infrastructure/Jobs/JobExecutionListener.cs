using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using TaskScheduler.Core.Entities;
using TaskScheduler.Core.Interfaces;
using TaskScheduler.Infrastructure.Helper;

namespace TaskScheduler.Infrastructure.Jobs;

public class JobExecutionListener : IJobListener
{
    public string Name => "GlobalJobListener";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutionListener> _logger;

    // Keyed by FireInstanceId — unique per execution
    private readonly ConcurrentDictionary<string, string> _executionHistoryMap = new ConcurrentDictionary<string, string>();

    public JobExecutionListener(IServiceProvider serviceProvider, ILogger<JobExecutionListener> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var historyRepo = scope.ServiceProvider.GetRequiredService<IExecutionHistoryRepository>();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

        var taskId = context.JobDetail.GetTaskId();
        ScheduledTask task = await taskRepo.GetByIdAsync(taskId);

        if (task == null)
        {
            _logger.LogWarning("[{TaskName}] No ScheduledTask found, skipping history tracking", taskId);
            return;
        }

        var history = await historyRepo.CreateAsync(new TaskExecutionHistory
        {
            TaskId = task.Id,
            StartTime = DateTime.UtcNow,
            Status = ExecutionStatus.Running
        });

        _executionHistoryMap[context.FireInstanceId] = history.Id;
        _logger.LogInformation("[{TaskName}] Started. HistoryId: {HistoryId}", context.JobDetail.GetTaskName(), history.Id);
    }

    public async Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
    {
        var taskName = context.JobDetail.GetTaskName();

        if (!_executionHistoryMap.TryRemove(context.FireInstanceId, out var historyId))
        {
            _logger.LogWarning("[{TaskName}] No historyId found for job, skipping history update", taskName);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var historyRepo = scope.ServiceProvider.GetRequiredService<IExecutionHistoryRepository>();

        var history = await historyRepo.GetByIdAsync(historyId);
        if (history == null) return;

        var endTime = DateTime.UtcNow;
        history.EndTime = endTime;
        history.DurationMs = (long)(endTime - history.StartTime).TotalMilliseconds;

        if (jobException != null)
        {
            history.Status = ExecutionStatus.Failed;
            history.ErrorMessage = jobException.Message;
            _logger.LogError("[{TaskName}] Failed. Error: {Error}", taskName, jobException.Message);
        }
        else
        {
            history.Status = ExecutionStatus.Completed;
            _logger.LogInformation("[{TaskName}] Completed in {DurationMs}ms. HistoryId: {HistoryId}", taskName, history.DurationMs, history.Id);
        }

        await historyRepo.UpdateAsync(history);
    }

    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[{TaskName}] Was vetoed", context.JobDetail.GetTaskName());
        return Task.CompletedTask;
    }
}