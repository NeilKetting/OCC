using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Infrastructure;

namespace OCC.Client.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly UpdateManager? _mgr;
        private readonly string _updateUrl = "https://github.com/NeilKetting/OrangeCircleConstruction";
        private readonly ILogger<UpdateService> _logger;

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

        public UpdateService(ILogger<UpdateService> logger)
        {
            _logger = logger;
            try
            {
                // We default to a SimpleWebSource. Ideally this is config driven.
                // For GitHub, use new GithubSource("url", "token", prerelease)
                
                _logger.LogInformation("Initializing UpdateManager...");
                
                 // We detect if it's a GitHub URL and use the proper source
                 if (_updateUrl.Contains("github.com"))
                 {
                    _logger.LogInformation($"Using GithubSource with URL: {_updateUrl}");
                    _mgr = new UpdateManager(new GithubSource(_updateUrl, null, false));
                 }
                 else
                 {
                     _logger.LogInformation($"Using SimpleWebSource with URL: {_updateUrl}");
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
                // Likely running in debug/not installed
                _mgr = null;
            }
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            if (_mgr == null || !_mgr.IsInstalled) 
            {
                _logger.LogWarning("CheckForUpdatesAsync skipped: UpdateManager not installed or null.");
                return null;
            }

            try
            {
                _logger.LogInformation("Checking for updates...");
                var updateInfo = await _mgr.CheckForUpdatesAsync();
                
                if (updateInfo == null)
                {
                     _logger.LogInformation("No updates found.");
                     return null;
                }

                _logger.LogInformation($"Update found. Target Version: {updateInfo.TargetFullRelease.Version}");

                // SAFETY CHECK: Prevent update loops
                // Ensure the target version is actually newer than what we are running.
                if (_mgr.CurrentVersion != null)
                {
                    if (updateInfo.TargetFullRelease.Version <= _mgr.CurrentVersion)
                    {
                        _logger.LogInformation($"Update version {updateInfo.TargetFullRelease.Version} is <= current version {_mgr.CurrentVersion}. Skipping.");
                        return null;
                    }
                }

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
             
            try 
            {
                _logger.LogInformation($"Downloading update {newVersion.TargetFullRelease.Version}...");
                await _mgr.DownloadUpdatesAsync(newVersion, progress);
                _logger.LogInformation("Download complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading updates.");
                throw;
            }
        }

        public void ApplyUpdatesAndExit(UpdateInfo newVersion)
        {
             _logger.LogInformation("Applying updates and restarting...");
             _mgr?.ApplyUpdatesAndRestart(newVersion);
        }
    }
}
