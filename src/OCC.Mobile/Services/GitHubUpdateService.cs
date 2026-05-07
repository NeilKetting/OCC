using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace OCC.Mobile.Services
{
    public class GitHubUpdateService : IUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GitHubUpdateService> _logger;
        private const string RepoOwner = "NeilKetting";
        private const string RepoName = "OCC.Mobile";

        public string CurrentVersion => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        public GitHubUpdateService(HttpClient httpClient, ILogger<GitHubUpdateService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // GitHub API requires a User-Agent
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OCC.Mobile", "1.0.0"));
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                _logger.LogInformation("Checking for updates on GitHub...");
                var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"GitHub API returned {response.StatusCode}");
                    return new UpdateCheckResult { IsUpdateAvailable = false };
                }

                var content = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(content);

                if (release == null || string.IsNullOrEmpty(release.TagName))
                    return new UpdateCheckResult { IsUpdateAvailable = false };

                var latestVersionStr = release.TagName.TrimStart('v');
                if (Version.TryParse(latestVersionStr, out var latestVersion) && 
                    Version.TryParse(CurrentVersion, out var currentVersion))
                {
                    if (latestVersion > currentVersion)
                    {
                        // Find the APK asset
                        var apkAsset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));
                        
                        return new UpdateCheckResult
                        {
                            IsUpdateAvailable = true,
                            LatestVersion = latestVersionStr,
                            DownloadUrl = apkAsset?.BrowserDownloadUrl ?? string.Empty,
                            ReleaseNotes = release.Body
                        };
                    }
                }

                return new UpdateCheckResult { IsUpdateAvailable = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates.");
                return new UpdateCheckResult { IsUpdateAvailable = false };
            }
        }

        public async Task<string> DownloadUpdateAsync(UpdateCheckResult update, Action<double> progress)
        {
            if (string.IsNullOrEmpty(update.DownloadUrl))
                throw new ArgumentException("Download URL is empty.");

            var destinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "update.apk");

            using var response = await _httpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var bytesRead = 0L;

            while (true)
            {
                var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) break;

                await fileStream.WriteAsync(buffer, 0, read);
                bytesRead += read;

                if (canReportProgress)
                {
                    progress?.Invoke((double)bytesRead / totalBytes);
                }
            }

            return destinationPath;
        }

        private class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("body")]
            public string Body { get; set; } = string.Empty;

            [JsonPropertyName("assets")]
            public List<GitHubAsset> Assets { get; set; } = new();
        }

        private class GitHubAsset
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = string.Empty;
        }
    }
}
