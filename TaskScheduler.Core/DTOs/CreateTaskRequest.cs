using System.ComponentModel.DataAnnotations;
using TaskScheduler.Core.Entities;

namespace TaskScheduler.Core.DTOs;

public class CreateTaskRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    [Required]
    public JobType JobType { get; set; }

    [Required]
    public ScheduleType ScheduleType { get; set; }

    public string? CronExpression { get; set; }

    [Range(1, int.MaxValue)]
    public int? IntervalSeconds { get; set; }

    public bool DisallowConcurrent { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public string? ServerId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
