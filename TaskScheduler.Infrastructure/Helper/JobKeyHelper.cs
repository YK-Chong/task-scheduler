using Quartz;
using TaskScheduler.Core.Entities;

namespace TaskScheduler.Infrastructure.Helper
{
    public static class JobKeyHelper
    {
        public static JobKey GetJobKey(ScheduledTask task)
        {
            var group = !string.IsNullOrEmpty(task.ServerId) ? task.ServerId : "default";
            return new JobKey(task.Id, group);
        }

        public static TriggerKey GetTriggerKey(ScheduledTask task)
        {
            var group = !string.IsNullOrEmpty(task.ServerId) ? task.ServerId : "default";
            return new TriggerKey($"trigger-{task.Id}", group);
        }
    }
}