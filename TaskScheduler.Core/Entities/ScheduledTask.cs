namespace TaskScheduler.Core.Entities;

public class ScheduledTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JobType JobType { get; set; }
    public ScheduleType ScheduleType { get; set; }
    public string? CronExpression { get; set; }
    public int? IntervalSeconds { get; set; }
    public bool DisallowConcurrent { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public string? ServerId { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskExecutionHistory> ExecutionHistories { get; set; } = new List<TaskExecutionHistory>();
}