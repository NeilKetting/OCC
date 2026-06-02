using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using OCC.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Infrastructure
{
    public static class CrashDetector
    {
        private static string GetCrashFolder()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OCC", "crashes");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }

        public static void HandleCrash(Exception? ex, string source)
        {
            if (ex == null) return;

            try
            {
                // 1. Log to Serilog
                Log.Fatal(ex, "FATAL UNHANDLED EXCEPTION [{Source}]: {Message}", source, ex.Message);
                Log.CloseAndFlush();

                // 2. Save details locally
                var details = new CrashReportDetails
                {
                    Id = Guid.NewGuid(),
                    ExceptionMessage = ex.Message,
                    StackTrace = ex.StackTrace ?? string.Empty,
                    Source = source,
                    AppVersion = "1.6.0",
                    Platform = Environment.OSVersion.ToString(),
                    Timestamp = DateTime.UtcNow,
                    ActiveView = "Unknown"
                };

                var folder = GetCrashFolder();
                var filePath = Path.Combine(folder, $"crash-{Guid.NewGuid()}.json");
                var json = JsonSerializer.Serialize(details);
                File.WriteAllText(filePath, json);
            }
            catch (Exception writeEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write crash report: {writeEx.Message}");
            }
        }

        public static async Task UploadPendingCrashesAsync(IServiceProvider serviceProvider)
        {
            try
            {
                var bugService = serviceProvider.GetService<IBugReportService>();
                var authService = serviceProvider.GetService<IAuthService>();
                if (bugService == null || authService == null) return;

                var folder = GetCrashFolder();
                if (!Directory.Exists(folder)) return;

                var files = Directory.GetFiles(folder, "crash-*.json");
                if (files.Length == 0) return;

                var currentUser = authService.CurrentUser;

                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var details = JsonSerializer.Deserialize<CrashReportDetails>(json);
                        if (details == null)
                        {
                            File.Delete(file);
                            continue;
                        }

                        // Build BugReport
                        var report = new BugReport
                        {
                            Id = Guid.NewGuid(),
                            ReporterId = currentUser?.Id,
                            ReporterName = currentUser != null ? $"{currentUser.FirstName} {currentUser.LastName}".Trim() : "System (Crash)",
                            ReportedDate = details.Timestamp,
                            ViewName = string.IsNullOrWhiteSpace(details.ActiveView) || details.ActiveView == "Unknown"
                                ? $"Desktop Crash: {details.Source}"
                                : details.ActiveView,
                            Description = $"[DESKTOP CRASH DETECTED]\n" +
                                          $"Source: {details.Source}\n" +
                                          $"App Version: {details.AppVersion}\n" +
                                          $"Platform: {details.Platform}\n" +
                                          $"Timestamp: {details.Timestamp:yyyy-MM-dd HH:mm:ss} UTC\n\n" +
                                          $"Message: {details.ExceptionMessage}\n\n" +
                                          $"Stack Trace:\n{details.StackTrace}",
                            Type = BugReportType.Crash,
                            Status = "Open"
                        };

                        await bugService.SubmitBugAsync(report);
                        File.Delete(file);
                    }
                    catch (Exception uploadEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to upload desktop crash file {file}: {uploadEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning/uploading desktop crashes: {ex.Message}");
            }
        }
    }
}
