using TaskScheduler.Core.DTOs;
using TaskScheduler.Core.Entities;

namespace TaskScheduler.Core.Interfaces;

public interface ITaskRepository
{
    Task<ScheduledTask?> GetByIdAsync(string id);
    Task<PagedResult<ScheduledTask>> GetAllAsync(int page, int pageSize, string? jobTypeFilter, bool? isEnabledFilter);
    Task<ScheduledTask> CreateAsync(ScheduledTask task);
    Task<ScheduledTask> UpdateAsync(ScheduledTask task);
    Task DeleteAsync(string id);
    Task<ScheduledTask> GetByJobTypeAndServerIdAsync(JobType jobType, string? serverId);
    Task<List<ScheduledTask>> GetByJobTypeAsync(JobType jobType);
}