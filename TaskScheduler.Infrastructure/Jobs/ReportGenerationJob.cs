using Microsoft.Extensions.Logging;
using Quartz;
using TaskScheduler.Infrastructure.Helper;

namespace TaskScheduler.Infrastructure.Jobs;

public class ReportGenerationJob : IJob
{
    private readonly ILogger<ReportGenerationJob> _logger;

    public ReportGenerationJob(ILogger<ReportGenerationJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var taskName = context.JobDetail.GetTaskName();
        var reportType = context.JobDetail.JobDataMap.GetString("reportType") ?? "General";

        _logger.LogInformation("[{TaskName}] Generating {ReportType} report", taskName, reportType);

        await Task.Delay(15000);

        _logger.LogInformation("[{TaskName}] Finished generating report", taskName);
    }
}