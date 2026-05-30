using Microsoft.Extensions.Logging;
using Quartz;
using TaskScheduler.Infrastructure.Helper;

namespace TaskScheduler.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class HeartbeatJob : IJob
{
    private readonly ILogger<HeartbeatJob> _logger;

    public HeartbeatJob(ILogger<HeartbeatJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var endpoint = context.JobDetail.JobDataMap.GetString("endpoint") ?? "unknown";
        var taskName = context.JobDetail.GetTaskName();
        
        _logger.LogInformation("[{TaskName}] Sending heartbeat to {Endpoint}", taskName, endpoint);

        await Task.Delay(1000);

        _logger.LogInformation("[{TaskName}] Heartbeat sent successfully to {Endpoint}", taskName, endpoint);
    }
}