using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OCC.API.Services
{
    public class AuditLogCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AuditLogCleanupService> _logger;
        private readonly TimeSpan _cleanupTime = new TimeSpan(3, 0, 0); // 3:00 AM

        public AuditLogCleanupService(IServiceProvider serviceProvider, ILogger<AuditLogCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Audit Log Cleanup Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = CalculateDelayUntilNextCleanup();
                _logger.LogInformation("Next audit log cleanup scheduled in {Delay}", delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                    await PerformCleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the audit log cleanup process.");
                }
            }
        }

        private TimeSpan CalculateDelayUntilNextCleanup()
        {
            var now = DateTime.Now;
            var nextRun = now.Date + _cleanupTime;
            if (now >= nextRun)
            {
                nextRun = nextRun.AddDays(1);
            }
            return nextRun - now;
        }

        private async Task PerformCleanupAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting audit log cleanup...");

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                try
                {
                    var setting = await dbContext.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "CompanyProfile", cancellationToken);
                    if (setting == null || string.IsNullOrEmpty(setting.Value))
                    {
                        _logger.LogWarning("CompanyProfile setting not found. Skipping audit log cleanup.");
                        return;
                    }

                    CompanyDetails? details = null;
                    try
                    {
                        details = JsonSerializer.Deserialize<CompanyDetails>(setting.Value, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize CompanyProfile setting.");
                    }

                    if (details == null) return;

                    var retentionMonths = details.AuditLogRetentionMonths;
                    if (retentionMonths <= 0)
                    {
                        _logger.LogInformation("Audit log retention is disabled (set to keep forever). No purge executed.");
                        return;
                    }

                    var cutoff = DateTime.UtcNow.AddMonths(-retentionMonths);
                    _logger.LogInformation("Purging audit logs older than {Cutoff} ({Months} months retention)...", cutoff, retentionMonths);

                    var deletedCount = await dbContext.Database.ExecuteSqlRawAsync(
                        "DELETE FROM AuditLogs WHERE Timestamp < {0}", 
                        new object[] { cutoff }
                    );

                    _logger.LogInformation("Purged {Count} audit log records successfully.", deletedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute audit log cleanup in database.");
                }
            }
        }
    }
}
