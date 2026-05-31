using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;
using Serilog.Events;
using TaskScheduler.Core.Interfaces;
using TaskScheduler.Core.Services;
using TaskScheduler.Infrastructure.Data;
using TaskScheduler.Infrastructure.Jobs;
using TaskScheduler.Infrastructure.Repositories;
using TaskScheduler.Infrastructure.Scheduler;

// Serilog Logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Quartz", LogEventLevel.Information)
    .WriteTo.Console()
    .WriteTo.File("logs/taskscheduler-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog();

    // Database
    var connectionString = builder.Configuration.GetConnectionString("Default");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
    );

    // Repositories
    builder.Services.AddScoped<ITaskRepository, TaskRepository>();
    builder.Services.AddScoped<IExecutionHistoryRepository, ExecutionHistoryRepository>();
    builder.Services.AddScoped<ITradingServerRepository, TradingServerRepository>();

    // Services
    builder.Services.AddScoped<ITaskService, TaskService>();
    builder.Services.AddScoped<ITradingServerService, TradingServerService>();
    builder.Services.AddScoped<ITaskScheduler, QuartzTaskScheduler>();

    // Quartz
    builder.Services.AddQuartz(q =>
    {
        q.UsePersistentStore(store =>
        {
            store.UseProperties = true;
            store.UseClustering();
            store.UseJsonSerializer();
            store.UseMySqlConnector(db =>
            {
                db.ConnectionString = connectionString!;
                db.TablePrefix = "QRTZ_";
            });
        });
    });

    builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

    // Register listener as singleton so Quartz can use it
    builder.Services.AddSingleton<JobExecutionListener>();

    // Swagger
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // Serialize enums as strings in JSON
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter()
            );
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Task Scheduler API", Version = "v1" });
        c.UseInlineDefinitionsForEnums();

        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    // Build App
    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        // Auto migrate
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Mark any orphaned Running records as Failed (caused by unexpected shutdown)
        var historyRepo = scope.ServiceProvider.GetRequiredService<IExecutionHistoryRepository>();
        var runningHistories = await historyRepo.GetByStatusAsync(TaskScheduler.Core.Entities.ExecutionStatus.Running);
        foreach (var history in runningHistories)
        {
            history.Status = TaskScheduler.Core.Entities.ExecutionStatus.Failed;
            history.EndTime = DateTime.UtcNow;
            history.DurationMs = (long)(DateTime.UtcNow - history.StartTime).TotalMilliseconds;
            history.ErrorMessage = "Application terminated unexpectedly";
            await historyRepo.UpdateAsync(history);
        }
        if (runningHistories.Count > 0)
            Log.Warning("Marked {Count} orphaned Running history records as Failed", runningHistories.Count);

        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var taskService = scope.ServiceProvider.GetRequiredService<ITaskService>();

        // Schedule MasterServerSyncJob
        var masterTask = await taskRepo.GetByJobTypeAndServerIdAsync(
            TaskScheduler.Core.Entities.JobType.MasterServerSyncJob, null);

        if (masterTask == null)
        {
            var masterIntervalSeconds = app.Configuration.GetValue<int>("JobSettings:MasterServerSyncJob:IntervalSeconds");
            if (masterIntervalSeconds <= 0) masterIntervalSeconds = 3600;

            await taskService.CreateTaskAsync(new TaskScheduler.Core.DTOs.CreateTaskRequest
            {
                Name = "MasterServerSyncJob",
                Description = "Dynamically create / remove SymbolDataPullJob for each server",
                JobType = TaskScheduler.Core.Entities.JobType.MasterServerSyncJob,
                ScheduleType = TaskScheduler.Core.Entities.ScheduleType.Simple,
                IntervalSeconds = masterIntervalSeconds,
                DisallowConcurrent = true,
                IsEnabled = true
            });

            Log.Information("MasterServerSyncJob seeded and scheduled");
        }
        else
        {
            Log.Information("MasterServerSyncJob already exists, skipping seed");
        }
    }

    // Register Quartz Listener
    var listenerSchedulerFactory = app.Services.GetRequiredService<ISchedulerFactory>();
    var listenerScheduler = await listenerSchedulerFactory.GetScheduler();
    var listener = app.Services.GetRequiredService<JobExecutionListener>();
    listenerScheduler.ListenerManager.AddJobListener(listener);

    // Middleware
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("Task Scheduler API starting...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    await Log.CloseAndFlushAsync();
}