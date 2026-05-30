using TaskScheduler.Core.DTOs;

namespace TaskScheduler.Core.Interfaces;

public interface ITaskService
{
    Task<TaskResponse> CreateTaskAsync(CreateTaskRequest request);
    Task<PagedResult<TaskResponse>> GetTasksAsync(int page, int pageSize, string? jobTypeFilter, bool? isEnabledFilter);
    Task<TaskResponse?> GetTaskByIdAsync(string id);
    Task<TaskResponse?> UpdateTaskAsync(string id, UpdateTaskRequest request);
    Task<bool> DeleteTaskAsync(string id);
    Task<TaskStatusResponse?> GetTaskStatusAsync(string id);
    Task<bool> TriggerTaskAsync(string id);
    Task RescheduleTaskAsync(string id);
}