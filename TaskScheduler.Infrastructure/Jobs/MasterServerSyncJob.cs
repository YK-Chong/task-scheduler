using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using TaskScheduler.Core.Interfaces;
using TaskScheduler.Infrastructure.Helper;

namespace TaskScheduler.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class MasterServerSyncJob : IJob
{
    private readonly ILogger<MasterServerSyncJob> _logger;
    private readonly IServiceProvider _serviceProvider;

    public MasterServerSyncJob(
        ILogger<MasterServerSyncJob> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var serverRepo = scope.ServiceProvider.GetRequiredService<ITradingServerRepository>();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var taskService = scope.ServiceProvider.GetRequiredService<ITaskService>();
        var taskScheduler = scope.ServiceProvider.GetRequiredService<ITaskScheduler>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var allServers = await serverRepo.GetAllAsync();
        var serverNameById = allServers.ToDictionary(s => s.Id, s => s.Name);
        var enabledServers = allServers.Where(s => s.IsEnabled).ToList();
        var disabledServers = allServers.Where(s => !s.IsEnabled).ToList();
        var enabledServerIds = enabledServers.Select(s => s.Id).ToHashSet();
        var taskName = context.JobDetail.GetTaskName();

        _logger.LogInformation("[{TaskName}] Syncing — {EnabledCount} enabled server(s), {DisabledCount} disabled server(s)",
            taskName, enabledServers.Count, disabledServers.Count);

        // Ensure jobs exist for all enabled servers
        foreach (var server in enabledServers)
        {
            var serverName = serverNameById.GetValueOrDefault(server.Id, server.Id);

            var existingTask = await taskRepo.GetByJobTypeAndServerIdAsync(
                Core.Entities.JobType.SymbolDataPullJob, server.Id);

            if (existingTask != null)
            {
                if (await taskScheduler.CheckExistsAsync(existingTask))
                {
                    continue;
                }

                // Reschedule if job is missing
                await taskService.RescheduleTaskAsync(existingTask.Id);
                _logger.LogInformation("[{TaskName}] Rescheduled missing job for server - {ServerName}", taskName, serverName);
                continue;
            }

            var symbolDataPullIntervalSeconds = configuration.GetValue<int>("JobSettings:SymbolDataPullJob:IntervalSeconds");
            if (symbolDataPullIntervalSeconds <= 0) symbolDataPullIntervalSeconds = 300;

            await taskService.CreateTaskAsync(new Core.DTOs.CreateTaskRequest
            {
                Name = $"SymbolDataPullJob-{serverName}",
                Description = $"Auto-created symbol pull job for {serverName}",
                JobType = Core.Entities.JobType.SymbolDataPullJob,
                ScheduleType = Core.Entities.ScheduleType.Simple,
                IntervalSeconds = symbolDataPullIntervalSeconds,
                DisallowConcurrent = true,
                IsEnabled = true,
                ServerId = server.Id
            });

            _logger.LogInformation("[{TaskName}] Created job for server - {ServerName}", taskName, serverName);
        }

        // Remove jobs for disabled servers
        var allSymbolTasks = await taskRepo.GetByJobTypeAsync(Core.Entities.JobType.SymbolDataPullJob);

        foreach (var task in allSymbolTasks.Where(t => t.ServerId != null && !enabledServerIds.Contains(t.ServerId)))
        {
            await taskService.DeleteTaskAsync(task.Id);
            var serverName = serverNameById!.GetValueOrDefault(task.ServerId, task.ServerId);
            _logger.LogInformation("[{TaskName}] Removed job for disabled server - {ServerName}", taskName, serverName);
        }

        _logger.LogInformation("[{TaskName}] Sync complete", taskName);
    }
}
