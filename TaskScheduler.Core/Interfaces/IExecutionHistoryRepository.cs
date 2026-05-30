using TaskScheduler.Core.Entities;

namespace TaskScheduler.Core.Interfaces;

public interface IExecutionHistoryRepository
{
    Task<TaskExecutionHistory> CreateAsync(TaskExecutionHistory history);
    Task<TaskExecutionHistory> GetByIdAsync(string id);
    Task<TaskExecutionHistory> UpdateAsync(TaskExecutionHistory history);
    Task<List<TaskExecutionHistory>> GetByTaskIdAsync(string taskId, int limit = 20);
    Task<List<TaskExecutionHistory>> GetByStatusAsync(ExecutionStatus status);
}