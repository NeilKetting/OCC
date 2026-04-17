-- LIVE SERVER DATA PRESERVATION SCRIPT
-- Run this on the live server to sync the schema without losing data.
-- This script adds the new columns and updates the migration history.

BEGIN TRANSACTION;

-- 1. Add new detailed address columns to Projects
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Projects]') AND name = N'StreetLine1')
BEGIN
    ALTER TABLE [Projects] ADD [StreetLine1] nvarchar(max) NOT NULL DEFAULT N'';
    ALTER TABLE [Projects] ADD [StreetLine2] nvarchar(max) NULL;
    ALTER TABLE [Projects] ADD [City] nvarchar(max) NOT NULL DEFAULT N'';
    ALTER TABLE [Projects] ADD [StateOrProvince] nvarchar(max) NOT NULL DEFAULT N'';
    ALTER TABLE [Projects] ADD [PostalCode] nvarchar(max) NOT NULL DEFAULT N'';
    ALTER TABLE [Projects] ADD [Country] nvarchar(max) NOT NULL DEFAULT N'';
    
    -- Optional: Copy old Location data to StreetLine1 for existing records
    EXEC('UPDATE [Projects] SET [StreetLine1] = [Location] WHERE [Location] IS NOT NULL AND [Location] <> ''''');
END

-- 2. Add PlannedDurationHours to ProjectTasks
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProjectTasks]') AND name = N'PlannedDurationHours')
BEGIN
    ALTER TABLE [ProjectTasks] ADD [PlannedDurationHours] bigint NULL;
    
    -- Optional: If you had data in PlanedDurationHours (typo), copy it over
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProjectTasks]') AND name = N'PlanedDurationHours')
    BEGIN
        EXEC('UPDATE [ProjectTasks] SET [PlannedDurationHours] = [PlanedDurationHours]');
    END
END

-- 3. Add ParentId to ProjectTasks (for subtasks)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProjectTasks]') AND name = N'ParentId')
BEGIN
    ALTER TABLE [ProjectTasks] ADD [ParentId] uniqueidentifier NULL;
    ALTER TABLE [ProjectTasks] ADD CONSTRAINT [FK_ProjectTasks_ProjectTasks_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [ProjectTasks] ([Id]);
END

-- 4. Create TaskAssignments table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[TaskAssignments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [TaskAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [AssigneeId] uniqueidentifier NOT NULL,
        [AssigneeType] int NOT NULL,
        [AssigneeName] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TaskAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskAssignments_ProjectTasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [ProjectTasks] ([Id]) ON DELETE NO ACTION
    );
END

-- 5. Create TaskComments table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[TaskComments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [TaskComments] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [ProjectTaskId] uniqueidentifier NULL,
        [AuthorName] nvarchar(max) NOT NULL,
        [AuthorEmail] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TaskComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskComments_ProjectTasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [ProjectTasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskComments_ProjectTasks_ProjectTaskId] FOREIGN KEY ([ProjectTaskId]) REFERENCES [ProjectTasks] ([Id])
    );
END

-- 6. Add ScopeOfWork to Orders
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Orders]') AND name = N'ScopeOfWork')
BEGIN
    ALTER TABLE [Orders] ADD [ScopeOfWork] nvarchar(max) NOT NULL DEFAULT N'';
END

-- 7. Add PhysicalAddress to Employees
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = N'PhysicalAddress')
BEGIN
    ALTER TABLE [Employees] ADD [PhysicalAddress] nvarchar(max) NOT NULL DEFAULT N'';
END

-- 3. Update Migration History to "Bless" the new InitialCreate
-- We delete all old history and insert the new consolidated baseline.
DELETE FROM [__EFMigrationsHistory];
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260118060127_InitialCreate', N'9.0.0');

COMMIT;

-- [WARNING]
-- The following columns are now legacy and can be removed AFTER you verify data migration:
-- ALTER TABLE Projects DROP COLUMN Location;
-- ALTER TABLE ProjectTasks DROP COLUMN AssignedTo;
-- ALTER TABLE ProjectTasks DROP COLUMN PlanedDurationHours;
