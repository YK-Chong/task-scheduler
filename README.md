# Task Scheduler

A distributed task scheduling system that manages automated jobs across multiple trading servers. Supports dynamic task creation, concurrent execution control, and real-time execution monitoring through a REST API — built with **.NET 8**, **Quartz.NET**, and **MySQL**.

---

## Table of Contents

- [Tech Stack](#tech-stack)
- [Architecture Overview](#architecture-overview)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [API Reference](#api-reference)
- [Job Types](#job-types)
- [Key Design Decisions](#key-design-decisions)
- [Trade-offs](#trade-offs)
- [Assumptions](#assumptions)

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 |
| Scheduler | Quartz.NET 3.13 |
| ORM | Entity Framework Core 8 |
| Database | MySQL 8 (via Pomelo) |
| Logging | Serilog |
| API Docs | Swagger / OpenAPI |

---

## Architecture Overview

The solution follows a clean architecture pattern split into three projects:

```
TaskScheduler.Core           # Entities, DTOs, Interfaces, Services
TaskScheduler.Infrastructure # Quartz jobs, repositories, EF Core, QuartzTaskScheduler
TaskScheduler.API            # ASP.NET Core controllers, DI wiring, startup
```

### Diagram

```
┌─────────────────────────────────────────────────────────┐
│                        API Layer                        │
│         TasksController  │  TradingServersController    │
└─────────────────┬────────────────────┬──────────────────┘
                  │                    │
┌─────────────────▼────────────────────▼──────────────────┐
│                       Core Layer                        │
│                                                         │
│   TaskService          TradingServerService             │
│       │                       │                         │
│   ITaskRepository   ITradingServerRepository            │
│   IExecutionHistoryRepository                           │
│   ITaskScheduler                                        │
└─────────────────┬───────────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────────┐
│                  Infrastructure Layer                   │
│                                                         │
│  TaskRepository        QuartzTaskScheduler              │
│  TradingServerRepository    │                           │
│  ExecutionHistoryRepository │                           │
│                        ┌────▼──────────────────┐        │
│                        │   Quartz Scheduler    │        │
│                        │  HeartbeatJob         │        │
│                        │  ReportGenerationJob  │        │
│                        │  SymbolDataPullJob    │        │
│                        │  MasterServerSyncJob  │        │
│                        └───────────────────────┘        │
│                                                         │
│  AppDbContext (EF Core)                                 │
└─────────────────┬───────────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────────┐
│                      MySQL Database                     │
│  ScheduledTasks  │  TaskExecutionHistories  │           │
│  TradingServers  │  QRTZ_* (Quartz tables)  │           │
└─────────────────────────────────────────────────────────┘
```

### Execution Flow

1. A task is created via `POST /api/tasks` — saved to DB and scheduled in Quartz.
2. Quartz fires the job at the configured interval or cron schedule.
3. `JobExecutionListener` intercepts every job execution and persists a `TaskExecutionHistory` record with start time, end time, duration, and status.
4. `MasterServerSyncJob` runs on a schedule and automatically creates or removes `SymbolDataPullJob` instances based on which trading servers are currently enabled.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- MySQL 8.0+

---

## Getting Started

### 1. Clone the repository

```bash
git clone <repository-url>
cd TaskScheduler
```

### 2. Configure the database connection

Edit `TaskScheduler.API/appsettings.Development.json` and update the connection string with your local credentials:

```json
"ConnectionStrings": {
  "Default": "Server=localhost;Port=3306;Database=taskscheduler;User=root;Password=yourpassword;"
}
```

> `appsettings.Development.json` is gitignored and safe for real credentials. Do not put your password in `appsettings.json`.

### 3. Create the database

```bash
mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS taskscheduler;"
```

> The application tables are created automatically via EF Core migrations on startup — no manual step required:
> - `ScheduledTasks` — stores task configuration (schedule, job type, metadata)
> - `TaskExecutionHistories` — stores per-execution records (start time, end time, duration, status)
> - `TradingServers` — stores registered trading servers and their enabled/disabled state
>
> For reference, the full application schema SQL is available at `database/migrations.sql`.

### 4. Create the Quartz.NET schema

Quartz requires its own tables for persistent job storage. Download and run the official MySQL schema script:

```bash
# Download from https://github.com/quartznet/quartznet/blob/main/database/tables/tables_mysql_innodb.sql
mysql -u root -p taskscheduler < tables_mysql_innodb.sql
```

### 5. Run the application

```bash
cd TaskScheduler.API
dotnet run
```

Swagger UI: `http://localhost:{port}/swagger`

On first startup the application will automatically run EF Core migrations and seed the `MasterServerSyncJob`.

Logs are written to the console and to `logs/taskscheduler-YYYYMMDD.log` in the project root. Job execution logs follow the format `[TaskName] message`, for example:

```
[MasterServerSyncJob] Started. HistoryId: a3f1c2d4-...
[MasterServerSyncJob] Syncing — 3 enabled server(s), 1 disabled server(s)
[MasterServerSyncJob] Sync complete
[MasterServerSyncJob] Completed in 523ms. HistoryId: a3f1c2d4-...
```

`HistoryId` is included in the start and completion logs to correlate each execution entry in the database, making it easy to trace a specific run from log to history record.

### 6. Create trading servers (optional)

This step is optional but required to demonstrate the dynamic task management feature. Trading servers must be created manually before `SymbolDataPullJob` instances are generated. Use the API or Swagger UI:

```json
POST /api/trading-servers
{
  "name": "ServerA"
}
```

Once created, `MasterServerSyncJob` will automatically create a `SymbolDataPullJob` for each enabled server on its next trigger.

---

## Configuration

Job intervals are configured under `JobSettings` in `appsettings.json`, with shorter development overrides in `appsettings.Development.json`.

| Key | Production | Development | Description |
|---|---|---|---|
| `JobSettings:MasterServerSyncJob:IntervalSeconds` | `3600` | `30` | How often the master job syncs servers |
| `JobSettings:SymbolDataPullJob:IntervalSeconds` | `300` | `10` | How often each server's pull job runs |

---

## API Reference

> All request payload schemas and response models are available interactively via Swagger UI at `http://localhost:{port}/swagger`.

### Tasks

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/tasks` | Create a new scheduled task |
| `GET` | `/api/tasks` | List tasks (supports `page`, `pageSize`, `jobType`, `isEnabled` filters) |
| `GET` | `/api/tasks/{id}` | Get a task by ID |
| `PUT` | `/api/tasks/{id}` | Update a task (partial update — only provided fields change) |
| `DELETE` | `/api/tasks/{id}` | Delete a task and unschedule it |
| `GET` | `/api/tasks/{id}/status` | Returns the current execution status and history of a task (latest 20 executions) |
| `POST` | `/api/tasks/{id}/trigger` | Immediately triggers a task outside of its normal schedule |

#### Create task — Simple schedule
```json
POST /api/tasks
{
  "name": "HeartbeatTask",
  "description": "Sends heartbeat every 5 minutes",
  "jobType": "HeartbeatJob",
  "scheduleType": "Simple",
  "intervalSeconds": 300,
  "disallowConcurrent": true,
  "isEnabled": true,
  "metadata": {
    "endpoint": "https://monitoring.example.com/heartbeat"
  }
}
```

#### Create task — Cron schedule
```json
POST /api/tasks
{
  "name": "DailyReportTask",
  "description": "Generate daily reports at 2 AM",
  "jobType": "ReportGenerationJob",
  "scheduleType": "Cron",
  "cronExpression": "0 0 2 * * ?",
  "disallowConcurrent": false,
  "isEnabled": true,
  "metadata": {
    "reportType": "Sales"
  }
}
```

> `SymbolDataPullJob` and `MasterServerSyncJob` are system-managed and cannot be created manually. The API returns `403 Forbidden` if attempted.

#### Get task status response
```json
GET /api/tasks/{id}/status

{
  "taskId": "b7e2a1f3-4c56-4d78-9e01-2f3a4b5c6d7e",
  "name": "HeartbeatTask",
  "currentStatus": "Completed",
  "lastTriggeredAt": "2025-02-14T10:30:00Z",
  "nextTriggerAt": "2025-02-14T10:35:00Z",
  "executionHistory": [
    {
      "executionId": "a3f1c2d4-7e89-4b12-9f34-1a2b3c4d5e6f",
      "startTime": "2025-02-14T10:30:00Z",
      "endTime": "2025-02-14T10:30:02Z",
      "status": "Completed",
      "durationMs": 2000,
      "errorMessage": null
    }
  ]
}
```

> Possible `currentStatus` values: `Pending` (never run) · `Running` · `Completed` · `Failed`

### Trading Servers

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/trading-servers` | List all servers |
| `POST` | `/api/trading-servers` | Create a new server |
| `PUT` | `/api/trading-servers/{id}/enable` | Enable a server |
| `PUT` | `/api/trading-servers/{id}/disable` | Disable a server |
| `DELETE` | `/api/trading-servers/{id}` | Delete a server |

---

## Job Types

| Job | Schedule | Concurrent | Description |
|---|---|---|---|
| `HeartbeatJob` | Simple | No | Simulates sending a heartbeat to an endpoint via a 1 second delay. Endpoint configured via `metadata.endpoint` |
| `ReportGenerationJob` | Cron | Configurable | Simulates report generation via a 15 second delay. Report type configured via `metadata.reportType` |
| `SymbolDataPullJob` | Simple | No | Simulates pulling symbol data for a specific trading server via a 1 second delay |
| `MasterServerSyncJob` | Simple | No | Syncs enabled servers and manages `SymbolDataPullJob` lifecycle |

---

## Key Design Decisions

**Repository Pattern** — All database access goes through repository interfaces. Services depend on abstractions, not EF Core directly, keeping the service layer decoupled from persistence.

**Scheduler Abstraction (`ITaskScheduler`)** — Quartz-specific scheduling logic is isolated behind an `ITaskScheduler` interface implemented by `QuartzTaskScheduler` in Infrastructure. This allows `TaskService` in Core to schedule, unschedule, and trigger jobs without any knowledge of Quartz. Swapping Quartz for another scheduler would only require a new `ITaskScheduler` implementation.

**Global Job Execution Listener** — Rather than embedding history-tracking in each job, a single `JobExecutionListener` intercepts all executions via Quartz's listener API. This keeps jobs focused on their actual work and centralises monitoring.

**Quartz Persistent Store with Clustering** — Quartz is configured with `UsePersistentStore` and `UseClustering()`. Jobs survive application restarts and the system can scale horizontally without duplicate executions.

**Idempotent Master Job** — `MasterServerSyncJob` checks the database before creating a `SymbolDataPullJob`. If the DB record exists but the Quartz job is missing (e.g. after a crash), it reschedules rather than duplicating. Running the master job multiple times is safe.

---

## Trade-offs

**Direct database deletion leaves orphaned Quartz job** — Deleting a task record directly from the database bypasses `DELETE /api/tasks/{id}`, leaving the Quartz job running with no corresponding task record. The job continues firing but `JobExecutionListener` detects the missing record and logs a warning each time. Since Quartz uses a persistent store, the orphaned job survives restarts and can only be removed by manually cleaning the `QRTZ_*` tables. Always use `DELETE /api/tasks/{id}` to ensure proper cleanup.

**Orphaned `Running` history on crash** — Execution history is tracked in memory via `JobExecutionListener`. If the application crashes while a job is running, the `JobWasExecuted` callback never fires, leaving the record stuck as `Running`. On the next startup, these orphaned records are automatically detected and marked as `Failed` with the message `"Application terminated unexpectedly"`.

---

## Assumptions

**`HeartbeatJob`, `MasterServerSyncJob`, and `SymbolDataPullJob` always run one at a time** — These jobs have `[DisallowConcurrentExecution]` applied directly on the class, so the `disallowConcurrent` flag on the API has no effect on them. Only `ReportGenerationJob` respects the flag.

**`MasterServerSyncJob` and `SymbolDataPullJob` cannot be created manually via the API** — Only one `MasterServerSyncJob` ever exists (seeded on startup). `SymbolDataPullJob` instances are created automatically by `MasterServerSyncJob` — exactly one per enabled server. Manual creation via `POST /api/tasks` is blocked with `403 Forbidden`. Update and delete operations can still be performed manually through the API.

**Server enable/disable changes are eventually consistent** — `SymbolDataPullJob` creation and removal is handled by `MasterServerSyncJob` on each trigger, not immediately when a server is enabled or disabled. This satisfies the assessment requirement for automatic task management, but with an eventual consistency delay rather than an immediate reaction.

**`MasterServerSyncJob` interval is seeded once from config** — The interval is read from `JobSettings:MasterServerSyncJob:IntervalSeconds` only on first startup. Changing the config value afterwards has no effect. To update the interval, use `PUT /api/tasks/{id}` with the new `intervalSeconds`, or call `DELETE /api/tasks/{id}` and restart the application to re-seed from config.

**Deleting an active server while its job is still running** — If a server is deleted while its `SymbolDataPullJob` is still running, the job detects the missing server record, logs a message and skips execution gracefully.

**All timestamps are UTC** — No timezone conversion is applied anywhere in the system.
