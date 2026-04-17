using Microsoft.Extensions.Logging;
using OCC.WpfClient.Services.Interfaces;
using Velopack;
using Velopack.Sources;

namespace OCC.WpfClient.Services
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
            if (_mgr == null || !_mgr.IsInstalled) 
            {
                return null;
            }

            try
            {
                _logger.LogInformation("Checking for updates...");
                var updateInfo = await _mgr.CheckForUpdatesAsync();
                
                if (updateInfo == null) return null;

                if (_mgr.CurrentVersion != null && updateInfo.TargetFullRelease.Version <= _mgr.CurrentVersion)
                {
                    return null;
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
            await _mgr.DownloadUpdatesAsync(newVersion, progress);
        }

        public void ApplyUpdatesAndRestart(UpdateInfo newVersion)
        {
            _mgr?.ApplyUpdatesAndRestart(newVersion);
        }
    }
}
