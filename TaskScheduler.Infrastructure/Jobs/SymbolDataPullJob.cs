using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using TaskScheduler.Core.Interfaces;
using TaskScheduler.Infrastructure.Helper;

namespace TaskScheduler.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class SymbolDataPullJob : IJob
{
    private readonly ILogger<SymbolDataPullJob> _logger;
    private readonly IServiceProvider _serviceProvider;

    public SymbolDataPullJob(ILogger<SymbolDataPullJob> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var serverRepo = scope.ServiceProvider.GetRequiredService<ITradingServerRepository>();

        var taskName = context.JobDetail.GetTaskName();
        var serverId = context.JobDetail.GetServerId();
        var server = await serverRepo.GetByIdAsync(serverId);

        if (server == null)
        {
            _logger.LogInformation("[{TaskName}] Server (ID: {ServerId}) has been deleted, skipping execution", taskName, serverId);
            return;
        }

        _logger.LogInformation("[{TaskName}] Pulling data for {ServerName}", taskName, server.Name);

        await Task.Delay(1000);

        _logger.LogInformation("[{TaskName}] Finished pulling data for {ServerName}", taskName, server.Name);
    }
}