using TaskScheduler.Core.Entities;

namespace TaskScheduler.Core.DTOs;

public class TaskResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JobType JobType { get; set; }
    public ScheduleType ScheduleType { get; set; }
    public string? CronExpression { get; set; }
    public int? IntervalSeconds { get; set; }
    public bool DisallowConcurrent { get; set; }
    public bool IsEnabled { get; set; }
    public string? ServerId { get; set; }
    public object? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
