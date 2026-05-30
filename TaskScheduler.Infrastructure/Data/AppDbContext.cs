using Microsoft.EntityFrameworkCore;
using TaskScheduler.Core.Entities;

namespace TaskScheduler.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();
    public DbSet<TaskExecutionHistory> TaskExecutionHistories => Set<TaskExecutionHistory>();
    public DbSet<TradingServer> TradingServers => Set<TradingServer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScheduledTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.JobType).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.ScheduleType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.CronExpression).HasMaxLength(100);
            entity.Property(e => e.ServerId).HasMaxLength(50);
            entity.Property(e => e.Metadata).HasColumnType("json");
        });

        modelBuilder.Entity<TaskExecutionHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.Task)
                  .WithMany(t => t.ExecutionHistories)
                  .HasForeignKey(e => e.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TradingServer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        });
    }
}