CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `ScheduledTasks` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NULL,
    `JobType` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `ScheduleType` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `CronExpression` varchar(100) CHARACTER SET utf8mb4 NULL,
    `IntervalSeconds` int NULL,
    `DisallowConcurrent` tinyint(1) NOT NULL,
    `IsEnabled` tinyint(1) NOT NULL,
    `ServerId` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Metadata` json NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ScheduledTasks` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `TradingServers` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `IsEnabled` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_TradingServers` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `TaskExecutionHistories` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `TaskId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `StartTime` datetime(6) NOT NULL,
    `EndTime` datetime(6) NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `DurationMs` bigint NULL,
    `ErrorMessage` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_TaskExecutionHistories` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_TaskExecutionHistories_ScheduledTasks_TaskId` FOREIGN KEY (`TaskId`) REFERENCES `ScheduledTasks` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_TaskExecutionHistories_TaskId` ON `TaskExecutionHistories` (`TaskId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260530050005_InitialCreate', '8.0.0');

COMMIT;

