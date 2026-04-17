using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;

namespace OCC.API.Services
{
    public class DatabaseBackupService : BackgroundService
    {
        private readonly ILogger<DatabaseBackupService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly string _backupFolder;
        private readonly int _retentionDays;
        private readonly TimeSpan _backupTime;

        public DatabaseBackupService(ILogger<DatabaseBackupService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            
            var backupSection = _configuration.GetSection("Backup");
            _backupFolder = backupSection.GetValue<string>("Path") ?? @"C:\Backups";
            _retentionDays = backupSection.GetValue<int>("RetentionDays", 7);
            
            var timeStr = backupSection.GetValue<string>("Time") ?? "02:00";
            if (!TimeSpan.TryParse(timeStr, out _backupTime))
            {
                _backupTime = new TimeSpan(2, 0, 0); // Default 02:00 AM
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Database Backup Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = CalculateDelayUntilNextBackup();
                _logger.LogInformation("Next backup scheduled in {Delay}", delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                    await PerformBackupAsync(stoppingToken);
                    CleanupOldBackups();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the backup process.");
                }
            }
        }

        private TimeSpan CalculateDelayUntilNextBackup()
        {
            var now = DateTime.Now;
            var nextRun = now.Date + _backupTime;
            if (now >= nextRun)
            {
                nextRun = nextRun.AddDays(1);
            }
            return nextRun - now;
        }

        private async Task PerformBackupAsync(CancellationToken cancellationToken)
        {
            var dbName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
            if (string.IsNullOrEmpty(dbName)) dbName = "MDK"; // Fallback or extract differently if needed

            if (!Directory.Exists(_backupFolder))
            {
                try
                {
                    Directory.CreateDirectory(_backupFolder);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create backup directory: {Path}", _backupFolder);
                    return;
                }
            }

            var fileName = $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            var filePath = Path.Combine(_backupFolder, fileName);
            
            // Note: BACKUP DATABASE command runs ON THE SQL SERVER. 
            // The path must be accessible by the SQL Server Service Account.
            // If the API stands on a different machine than SQL, this path refers to the SQL Server's local disk.
            
            var query = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH FORMAT, MEDIANAME = 'SQLServerBackups', NAME = 'Full Backup of {dbName}';";

            _logger.LogInformation("Starting backup for {DbName} to {Path}...", dbName, filePath);

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync(cancellationToken);
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        // SQL Server requires absolute path passed as parameter or string literal
                        cmd.Parameters.AddWithValue("@path", filePath);
                        // Increase timeout for large backups
                        cmd.CommandTimeout = 300; 
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
                _logger.LogInformation("Backup completed successfully: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup failed.");
                throw;
            }
        }

        private void CleanupOldBackups()
        {
            try
            {
                // This clean-up runs on the API server.
                // IF SQL Server is on a different machine, and _backupFolder refers to a local path,
                // we might be cleaning up a different folder or failing if it's a network share.
                // For this implementation, we assume API and SQL share the filesystem or _backupFolder is a valid path for both.
                
                if (!Directory.Exists(_backupFolder)) return;

                var directory = new DirectoryInfo(_backupFolder);
                var files = directory.GetFiles("*.bak");
                var cutoff = DateTime.Now.AddDays(-_retentionDays);

                foreach (var file in files)
                {
                    if (file.CreationTime < cutoff)
                    {
                        _logger.LogInformation("Deleting old backup: {FileName}", file.Name);
                        file.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while cleaning up old backups.");
            }
        }
    }
}
