using TaskScheduler.Core.Entities;

namespace TaskScheduler.Core.DTOs;

public class TaskStatusResponse
{
    public string TaskId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ExecutionStatus CurrentStatus { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime? NextTriggerAt { get; set; }
    public List<ExecutionHistoryItem> ExecutionHistory { get; set; } = new List<ExecutionHistoryItem>();
}

public class ExecutionHistoryItem
{
    public string ExecutionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ExecutionStatus Status { get; set; }
    public long? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}
