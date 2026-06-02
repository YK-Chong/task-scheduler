using Microsoft.EntityFrameworkCore;
using TaskScheduler.Core.DTOs;
using TaskScheduler.Core.Entities;
using TaskScheduler.Core.Interfaces;
using TaskScheduler.Infrastructure.Data;

namespace TaskScheduler.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ScheduledTask?> GetByIdAsync(string id)
    {
        return await _context.ScheduledTasks.FindAsync(id);
    }

    public async Task<PagedResult<ScheduledTask>> GetAllAsync(int page, int pageSize, string? jobTypeFilter, bool? isEnabledFilter)
    {
        var query = _context.ScheduledTasks.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(jobTypeFilter) && Enum.TryParse<JobType>(jobTypeFilter, true, out var jobType))
            query = query.Where(t => t.JobType == jobType);

        if (isEnabledFilter.HasValue)
            query = query.Where(t => t.IsEnabled == isEnabledFilter.Value);

        var totalCount = await query.CountAsync();

        var items = totalCount == 0
            ? new List<ScheduledTask>()
            : await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        return new PagedResult<ScheduledTask>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ScheduledTask> CreateAsync(ScheduledTask task)
    {
        _context.ScheduledTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<ScheduledTask> UpdateAsync(ScheduledTask task)
    {
        task.UpdatedAt = DateTime.UtcNow;
        _context.ScheduledTasks.Update(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task DeleteAsync(string id)
    {
        var task = await _context.ScheduledTasks.FindAsync(id);
        if (task != null)
        {
            _context.ScheduledTasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<ScheduledTask> GetByJobTypeAndServerIdAsync(JobType jobType, string? serverId)
    {
        return await _context.ScheduledTasks
            .FirstOrDefaultAsync(t => t.JobType == jobType && t.ServerId == serverId);
    }

    public async Task<List<ScheduledTask>> GetByJobTypeAsync(JobType jobType)
    {
        return await _context.ScheduledTasks
            .Where(t => t.JobType == jobType)
            .ToListAsync();
    }
}
