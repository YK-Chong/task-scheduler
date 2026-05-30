using Quartz;

namespace TaskScheduler.Infrastructure.Helper;

public static class JobDataMapExtensions
{
    public static string? GetTaskId(this IJobDetail jobDetail)
    {
        return jobDetail.JobDataMap.TryGetValue("taskId", out var value)
            ? value?.ToString()
            : null;
    }

    public static string? GetTaskName(this IJobDetail jobDetail)
    {
        return jobDetail.JobDataMap.TryGetValue("taskName", out var value)
            ? value?.ToString()
            : jobDetail.Key.Name;
    }

    public static string? GetServerId(this IJobDetail jobDetail)
    {
        return jobDetail.JobDataMap.TryGetValue("serverId", out var value)
            ? value?.ToString()
            : null;
    }

    public static string GetReportType(this IJobDetail jobDetail)
    {
        return jobDetail.JobDataMap.TryGetValue("reportType", out var value)
            ? value?.ToString() ?? "General"
            : "General";
    }
}