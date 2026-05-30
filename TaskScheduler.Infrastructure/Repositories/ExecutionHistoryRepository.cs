using Microsoft.EntityFrameworkCore;
using TaskScheduler.Core.Entities;
using TaskScheduler.Core.Interfaces;
using TaskScheduler.Infrastructure.Data;

namespace TaskScheduler.Infrastructure.Repositories;

public class ExecutionHistoryRepository : IExecutionHistoryRepository
{
    private readonly AppDbContext _context;

    public ExecutionHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TaskExecutionHistory> CreateAsync(TaskExecutionHistory history)
    {
        _context.TaskExecutionHistories.Add(history);
        await _context.SaveChangesAsync();
        return history;
    }

    public async Task<TaskExecutionHistory> GetByIdAsync(string id)
    {
        return await _context.TaskExecutionHistories.FindAsync(id);
    }

    public async Task<TaskExecutionHistory> UpdateAsync(TaskExecutionHistory history)
    {
        _context.TaskExecutionHistories.Update(history);
        await _context.SaveChangesAsync();
        return history;
    }

    public async Task<List<TaskExecutionHistory>> GetByTaskIdAsync(string taskId, int limit = 20)
    {
        return await _context.TaskExecutionHistories
            .Where(h => h.TaskId == taskId)
            .OrderByDescending(h => h.StartTime)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<TaskExecutionHistory>> GetByStatusAsync(ExecutionStatus status)
    {
        return await _context.TaskExecutionHistories
            .Where(h => h.Status == status)
            .ToListAsync();
    }
}
