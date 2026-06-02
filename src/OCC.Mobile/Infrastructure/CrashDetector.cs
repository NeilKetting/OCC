using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using OCC.Shared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OCC.Mobile.Services;

namespace OCC.Mobile.Infrastructure
{
    public static class CrashDetector
    {
        private static string GetCrashFolder()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OCC.Mobile", "crashes");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }

        public static void RegisterGlobalHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    HandleCrash(ex, "AppDomain.UnhandledException");
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                if (e.Exception != null)
                {
                    HandleCrash(e.Exception, "TaskScheduler.UnobservedTaskException");
                }
            };
        }

        public static void HandleCrash(Exception ex, string source)
        {
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
                    AppVersion = App.AppVersion,
                    Platform = Environment.OSVersion.ToString(),
                    Timestamp = DateTime.UtcNow,
                    ActiveView = "Unknown"
                };

                // Attempt to grab current active view name from MainViewModel if possible
                try
                {
                    var mainVm = App.Services?.GetService<OCC.Mobile.Features.Shell.MainViewModel>();
                    if (mainVm?.CurrentView != null)
                    {
                        details.ActiveView = mainVm.CurrentView.GetType().Name;
                    }
                }
                catch { /* Safeguard */ }

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

        public static async Task UploadPendingCrashesAsync(IServiceProvider? serviceProvider)
        {
            if (serviceProvider == null) return;

            try
            {
                var authService = serviceProvider.GetService<IAuthService>();
                if (authService == null) return;

                var folder = GetCrashFolder();
                if (!Directory.Exists(folder)) return;

                var files = Directory.GetFiles(folder, "crash-*.json");
                if (files.Length == 0) return;

                var baseUrl = authService.GetBaseUrl();
                var token = authService.CurrentToken;
                var currentUser = authService.CurrentUser;

                using var httpClient = new HttpClient();
                
                // Set custom environment header if needed
                var settingsService = serviceProvider.GetService<ILocalSettingsService>();
                if (settingsService != null)
                {
                    httpClient.DefaultRequestHeaders.Add("X-Environment", settingsService.Settings.SelectedEnvironment.ToString());
                }

                if (!string.IsNullOrEmpty(token))
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

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
                                ? $"Mobile Crash: {details.Source}" 
                                : details.ActiveView,
                            Description = $"[MOBILE CRASH DETECTED]\n" +
                                          $"Source: {details.Source}\n" +
                                          $"App Version: {details.AppVersion}\n" +
                                          $"Platform: {details.Platform}\n" +
                                          $"Timestamp: {details.Timestamp:yyyy-MM-dd HH:mm:ss} UTC\n\n" +
                                          $"Message: {details.ExceptionMessage}\n\n" +
                                          $"Stack Trace:\n{details.StackTrace}",
                            Type = BugReportType.Crash,
                            Status = "Open"
                        };

                        var response = await httpClient.PostAsJsonAsync($"{baseUrl}api/BugReports", report);
                        if (response.IsSuccessStatusCode)
                        {
                            File.Delete(file);
                        }
                        else
                        {
                            var err = await response.Content.ReadAsStringAsync();
                            System.Diagnostics.Debug.WriteLine($"Failed to upload crash: {response.StatusCode} - {err}");
                        }
                    }
                    catch (Exception uploadEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to upload crash file {file}: {uploadEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning/uploading crashes: {ex.Message}");
            }
        }
    }
}
