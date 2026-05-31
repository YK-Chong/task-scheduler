using TaskScheduler.Core.Entities;

namespace TaskScheduler.Core.Interfaces;

public interface ITaskScheduler
{
    Task ScheduleAsync(ScheduledTask task);
    Task UnscheduleAsync(ScheduledTask task);
    Task<bool> TriggerAsync(ScheduledTask task);
    Task<bool> CheckExistsAsync(ScheduledTask task);
    Task<DateTime?> GetNextFireTimeAsync(ScheduledTask task);
}
