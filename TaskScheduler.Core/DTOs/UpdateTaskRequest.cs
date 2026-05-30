namespace TaskScheduler.Core.DTOs;

public class UpdateTaskRequest
{
    public string? Description { get; set; }
    public string? CronExpression { get; set; }
    public int? IntervalSeconds { get; set; }
    public bool? DisallowConcurrent { get; set; }
    public bool? IsEnabled { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
