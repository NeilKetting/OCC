-- =============================================
-- CLONE LIVE DATABASE TO MAIN
-- =============================================
USE master;
GO

DECLARE @LiveDbName NVARCHAR(100) = N'OCC_Rev5_DB';
DECLARE @MainDbName NVARCHAR(100) = N'OCC_Main';
DECLARE @BackupPath NVARCHAR(500) = N'C:\OCCBackups\OCC_Live_Temp.bak';
DECLARE @MainDataPath NVARCHAR(500) = N'C:\Program Files\Microsoft SQL Server\MSSQL17.OCC_SQL\MSSQL\DATA\OCC_Main.mdf';
DECLARE @MainLogPath NVARCHAR(500) = N'C:\Program Files\Microsoft SQL Server\MSSQL17.OCC_SQL\MSSQL\DATA\OCC_Main_log.ldf';

-- 1. Kick everyone off Main DB (only if it exists)
IF EXISTS (SELECT name FROM sys.databases WHERE name = @MainDbName)
BEGIN
    PRINT 'Setting Main DB to Single User Mode...';
    DECLARE @sql NVARCHAR(MAX);
    SET @sql = N'ALTER DATABASE [' + @MainDbName + N'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE';
    EXEC sp_executesql @sql;
END
ELSE
BEGIN
    PRINT 'Main DB does not exist yet. It will be created by the restore process.';
END

-- 2. Backup Live
PRINT 'Backing up Live DB...';
-- Enable explicit transaction handling for safety
SET XACT_ABORT ON;

BACKUP DATABASE @LiveDbName 
TO DISK = @BackupPath 
WITH FORMAT, INIT, CHECKSUM, STATS = 10, NAME = N'Full Backup of Live for Main Sync';

-- 3. Restore to Main (This creates the DB if it doesn't exist)
PRINT 'Restoring to Main DB...';
DECLARE @LiveLogName NVARCHAR(128);
SET @LiveLogName = @LiveDbName + N'_log';

RESTORE DATABASE @MainDbName 
FROM DISK = @BackupPath 
WITH REPLACE, RECOVERY, CHECKSUM, STATS = 10,
MOVE @LiveDbName TO @MainDataPath,
MOVE @LiveLogName TO @MainLogPath;

-- 4. Bring Main back online
PRINT 'Setting Main DB back to Multi User Mode...';
DECLARE @sqlMulti NVARCHAR(MAX);
SET @sqlMulti = N'ALTER DATABASE [' + @MainDbName + N'] SET MULTI_USER';
EXEC sp_executesql @sqlMulti;

PRINT 'Synchronization Complete!';
GO
