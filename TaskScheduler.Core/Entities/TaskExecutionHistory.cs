namespace TaskScheduler.Core.Entities;

public class TaskExecutionHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TaskId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ExecutionStatus Status { get; set; }
    public long? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public ScheduledTask Task { get; set; }
}
