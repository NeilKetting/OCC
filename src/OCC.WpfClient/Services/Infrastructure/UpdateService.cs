using System.IO;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using Velopack;
using Velopack.Sources;

namespace OCC.WpfClient.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly UpdateManager? _mgr;
        private readonly string _updateUrl = "https://github.com/NeilKetting/OCC-ERP";
        private readonly ILogger<UpdateService> _logger;
        private readonly LocalSettingsService _localSettingsService;

        public string CurrentVersion
        {
            get
            {
                try
                {
                    var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
                    return _mgr?.CurrentVersion?.ToString() ?? assemblyVersion;
                }
                catch
                {
                    return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
                }
            }
        }

        public UpdateService(ILogger<UpdateService> logger, LocalSettingsService localSettingsService)
        {
            _logger = logger;
            _localSettingsService = localSettingsService;
            try
            {
                _logger.LogInformation("Initializing UpdateManager...");
                
                if (_updateUrl.Contains("github.com"))
                {
                    _mgr = new UpdateManager(new GithubSource(_updateUrl, null, false));
                }
                else
                {
                    _mgr = new UpdateManager(new SimpleWebSource(_updateUrl));
                }
                 
                if (_mgr.IsInstalled)
                {
                    _logger.LogInformation($"UpdateManager Initialized. Current Version: {_mgr.CurrentVersion}");
                }
                else
                {
                    _logger.LogWarning("UpdateManager is NOT Installed (likely running in debug/portable mode).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize UpdateManager.");
                _mgr = null;
            }
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            // Never check for updates while debugging to prevent loops
            if (System.Diagnostics.Debugger.IsAttached)
            {
                _logger.LogInformation("Skipping update check: Debugger is attached.");
                return null;
            }

            if (_mgr == null || !_mgr.IsInstalled) 
            {
                _logger.LogInformation("Skipping update check: App is not installed (Portable/Debug mode).");
                return null;
            }

            try
            {
                // Verify if previous update attempt succeeded
                var lastAttemptedVersion = _localSettingsService.Settings.LastAttemptedUpdateVersion;
                if (!string.IsNullOrEmpty(lastAttemptedVersion))
                {
                    var cleanCurrent = CurrentVersion.TrimStart('v', 'V').Split('-')[0];
                    var cleanLast = lastAttemptedVersion.TrimStart('v', 'V').Split('-')[0];
                    if (string.Equals(cleanCurrent, cleanLast, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Update to {Version} succeeded! Resetting update attempt counter.", CurrentVersion);
                        _localSettingsService.Settings.FailedUpdateAttemptCount = 0;
                        _localSettingsService.Settings.LastAttemptedUpdateVersion = string.Empty;
                        _localSettingsService.Save();
                    }
                }

                _logger.LogInformation("Checking for updates...");
                var updateInfo = await _mgr.CheckForUpdatesAsync();
                
                if (updateInfo == null || updateInfo.TargetFullRelease == null) return null;

                // Robust version comparison using fallback-aware CurrentVersion
                var localVersionStr = CurrentVersion;
                var remoteVersionStr = updateInfo.TargetFullRelease.Version.ToString();

                var cleanLocal = localVersionStr.TrimStart('v', 'V').Split('-')[0];
                var cleanRemote = remoteVersionStr.TrimStart('v', 'V').Split('-')[0];

                if (System.Version.TryParse(cleanLocal, out var localParsed) && 
                    System.Version.TryParse(cleanRemote, out var remoteParsed))
                {
                    if (remoteParsed <= localParsed)
                    {
                        _logger.LogInformation($"No new updates. Local: {localVersionStr}, Remote: {remoteVersionStr}");
                        return null;
                    }
                }

                // Check Circuit Breaker: Detect loop condition (>= 2 failed attempts for same version)
                if (string.Equals(_localSettingsService.Settings.LastAttemptedUpdateVersion, remoteVersionStr, StringComparison.OrdinalIgnoreCase))
                {
                    if (_localSettingsService.Settings.FailedUpdateAttemptCount >= 2)
                    {
                        _logger.LogWarning("UPDATE LOOP DETECTED for version {Version} (Attempts: {Count}). Executing automatic repair and skipping update.",
                            remoteVersionStr, _localSettingsService.Settings.FailedUpdateAttemptCount);

                        PurgeUpdateCache();

                        _localSettingsService.Settings.FailedUpdateAttemptCount = 0;
                        _localSettingsService.Settings.LastAttemptedUpdateVersion = string.Empty;
                        _localSettingsService.Save();

                        return null; // Skip update to let user use the app
                    }
                }

                _logger.LogInformation($"New update found! Local: {localVersionStr}, Remote: {remoteVersionStr}");
                return updateInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates.");
                return null;
            }
        }

        public async Task DownloadUpdatesAsync(UpdateInfo newVersion, Action<int> progress)
        {
            if (_mgr == null) return;
            await _mgr.DownloadUpdatesAsync(newVersion, progress);
        }

        public void ApplyUpdatesAndRestart(UpdateInfo newVersion)
        {
            if (_mgr == null || newVersion == null) return;

            var targetVersion = newVersion.TargetFullRelease?.Version.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(targetVersion))
            {
                if (string.Equals(_localSettingsService.Settings.LastAttemptedUpdateVersion, targetVersion, StringComparison.OrdinalIgnoreCase))
                {
                    _localSettingsService.Settings.FailedUpdateAttemptCount++;
                }
                else
                {
                    _localSettingsService.Settings.LastAttemptedUpdateVersion = targetVersion;
                    _localSettingsService.Settings.FailedUpdateAttemptCount = 1;
                }
                _localSettingsService.Save();
            }

            _mgr.ApplyUpdatesAndRestart(newVersion);
        }

        public void PurgeUpdateCache()
        {
            try
            {
                _logger.LogInformation("Purging Velopack package cache and temporary update files...");
                var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OCC-ERP");
                var packagesDir = Path.Combine(appDir, "packages");
                var tempDir = Path.Combine(appDir, "temp");
                var velopackTempDir = Path.Combine(Path.GetTempPath(), "Velopack");

                if (Directory.Exists(packagesDir))
                {
                    _logger.LogInformation("Removing package cache at {Dir}", packagesDir);
                    Directory.Delete(packagesDir, true);
                }
                if (Directory.Exists(tempDir))
                {
                    _logger.LogInformation("Removing temporary files at {Dir}", tempDir);
                    Directory.Delete(tempDir, true);
                }
                if (Directory.Exists(velopackTempDir))
                {
                    _logger.LogInformation("Removing Velopack temp files at {Dir}", velopackTempDir);
                    Directory.Delete(velopackTempDir, true);
                }

                _logger.LogInformation("Update cache purge complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purge update cache.");
            }
        }
    }
}
