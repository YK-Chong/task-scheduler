namespace TaskScheduler.Core.Entities;

public enum ScheduleType
{
    Simple,
    Cron
}

public enum ExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public enum JobType
{
    HeartbeatJob,
    ReportGenerationJob,
    SymbolDataPullJob,
    MasterServerSyncJob
}