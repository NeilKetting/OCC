using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;
using OCC.Client.Services.Infrastructure; // For ConnectionSettings
using CommunityToolkit.Mvvm.Messaging;

namespace OCC.Client.Services
{
    public class LogUploadService : ILogUploadService
    {
        private readonly IAuthService _authService;
        private readonly IUpdateService _updateService;

        public LogUploadService(IAuthService authService, IUpdateService updateService)
        {
            _authService = authService;
            _updateService = updateService;
        }

        public async Task UploadLogsAsync()
        {
            string? zipPath = null;
            try
            {
                var logsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OCC", "logs");
                if (!Directory.Exists(logsPath)) return;

                // 1. Collect Logs (Last 2 days as verified)
                // Use glob to find all log-*.txt
                var logFiles = Directory.GetFiles(logsPath, "log-*.txt")
                                        .Select(f => new FileInfo(f))
                                        .Where(f => f.LastWriteTime >= DateTime.Now.AddDays(-2)) // Safety check
                                        .ToList();

                if (!logFiles.Any()) return;

                zipPath = Path.Combine(Path.GetTempPath(), $"OCC_Logs_{Guid.NewGuid()}.zip");

                // 2. Zip
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var file in logFiles)
                    {
                        try 
                        {
                            // Copy to temp stream to avoid locking issues if log is Open
                            using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using (var entryStream = archive.CreateEntry(file.Name).Open())
                            {
                                await fs.CopyToAsync(entryStream);
                            }
                        }
                        catch { /* Skip locked/inaccessible files */ }
                    }
                }

                // 3. Gather Metadata
                var user = _authService.CurrentUser;
                var metadata = new LogUploadRequest
                {
                    UserId = user?.Id,
                    UserName = user?.DisplayName ?? "Unknown",
                    UserEmail = user?.Email ?? "Unknown",
                    UserRole = user?.UserRole.ToString() ?? "Unknown",
                    
                    MachineName = Environment.MachineName,
                    OSVersion = Environment.OSVersion.ToString(),
                    DotNetVersion = RuntimeInformation.FrameworkDescription,
                    ProcessorCount = Environment.ProcessorCount.ToString(),
                    // SystemMemory hard to get cross-platform reliably without extra libs, omitting for now or use GC heuristic
                    
                    AppVersion = _updateService.CurrentVersion,
                    Environment = ConnectionSettings.Instance.SelectedEnvironment.ToString(),
                    
                    CultureName = System.Globalization.CultureInfo.CurrentCulture.Name,
                    DatePattern = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern
                };

                // 4. Upload
                using var client = new HttpClient();
                client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl);
                
                // Add Authorization header if available
                if (!string.IsNullOrEmpty(_authService.AuthToken))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AuthToken);
                }

                using var content = new MultipartFormDataContent();
                
                content.Add(new StringContent(metadata.UserId?.ToString() ?? ""), nameof(LogUploadRequest.UserId));
                content.Add(new StringContent(metadata.UserName), nameof(LogUploadRequest.UserName));
                content.Add(new StringContent(metadata.UserEmail), nameof(LogUploadRequest.UserEmail));
                content.Add(new StringContent(metadata.UserRole), nameof(LogUploadRequest.UserRole));
                
                content.Add(new StringContent(metadata.MachineName), nameof(LogUploadRequest.MachineName));
                content.Add(new StringContent(metadata.OSVersion), nameof(LogUploadRequest.OSVersion));
                content.Add(new StringContent(metadata.DotNetVersion), nameof(LogUploadRequest.DotNetVersion));
                content.Add(new StringContent(metadata.ProcessorCount), nameof(LogUploadRequest.ProcessorCount));
                
                content.Add(new StringContent(metadata.AppVersion), nameof(LogUploadRequest.AppVersion));
                content.Add(new StringContent(metadata.Environment), nameof(LogUploadRequest.Environment));
                content.Add(new StringContent(metadata.Timestamp.ToString("o")), nameof(LogUploadRequest.Timestamp));
                
                content.Add(new StringContent(metadata.CultureName), nameof(LogUploadRequest.CultureName));
                content.Add(new StringContent(metadata.DatePattern), nameof(LogUploadRequest.DatePattern));

                // Add File with explicit content type
                using var fileStream = File.OpenRead(zipPath);
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                content.Add(fileContent, "file", "logs.zip");

                WeakReferenceMessenger.Default.Send(new Messages.LogUploadStatusMessage("Uploading logs...", true));

                var response = await client.PostAsync("api/Logs/upload", content);
                if (!response.IsSuccessStatusCode)
                {
                    await ApiLogging.LogFailureAsync("UploadLogs", response);
                }
                response.EnsureSuccessStatusCode();

                WeakReferenceMessenger.Default.Send(new Messages.LogUploadStatusMessage("Logs uploaded successfully", false, true));
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("UploadLogs", ex);
                WeakReferenceMessenger.Default.Send(new Messages.LogUploadStatusMessage("Upload failed: " + ex.Message, false, false, true));
                throw;
            }
            finally
            {
                if (zipPath != null && File.Exists(zipPath))
                {
                    try { File.Delete(zipPath); } catch { }
                }
            }
        }
        public async Task<System.Collections.Generic.List<LogUploadRequest>> GetLogsAsync()
        {
            using var client = new HttpClient { BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl) };
            // Add auth token if needed, but endpoint is restricted so we need it. 
            // However, current implementation of HttpClient factory in this service is instantiation. 
            // We should use an injected HttpClient or add headers. 
            // For now, let's just add the bearer token from AuthService if available.
            if (!string.IsNullOrEmpty(_authService.AuthToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AuthToken);
            }

            try
            {
                var response = await client.GetAsync("api/Logs/list");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<System.Collections.Generic.List<LogUploadRequest>>() ?? new System.Collections.Generic.List<LogUploadRequest>();
                }

                await ApiLogging.LogFailureAsync("GetLogs", response);
                return new System.Collections.Generic.List<LogUploadRequest>();
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("GetLogs", ex);
                return new System.Collections.Generic.List<LogUploadRequest>();
            }
        }

        public async Task DeleteLogAsync(Guid id)
        {
            try
            {
                using var client = new HttpClient();
                client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl);
                if (!string.IsNullOrEmpty(_authService.AuthToken))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AuthToken);
                }

            var response = await client.DeleteAsync($"api/Logs/{id}");
            if (!response.IsSuccessStatusCode)
            {
                await ApiLogging.LogFailureAsync("DeleteLog", response);
            }
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            ApiLogging.LogException("DeleteLog", ex);
            throw;
        }
    }

        public async Task<Stream> DownloadLogAsync(Guid id)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl);
            // Download is AllowAnonymous now for simplicity in verification, but good to send token
            if (!string.IsNullOrEmpty(_authService.AuthToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AuthToken);
            }

            var response = await client.GetAsync($"api/logs/download/{id}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }
    }
}
