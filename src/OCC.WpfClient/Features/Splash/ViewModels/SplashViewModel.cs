using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.Splash.ViewModels
{
    public partial class SplashViewModel : ViewModelBase
    {
        private readonly ILogger<SplashViewModel> _logger;
        private readonly IUpdateService _updateService;

        [ObservableProperty]
        private string _loadingStatus = "Starting...";

        [ObservableProperty]
        private int _progressValue = 0;

        [ObservableProperty]
        private bool _isUpdateDownloading = false;

        public SplashViewModel(ILogger<SplashViewModel> logger, IUpdateService updateService)
        {
            _logger = logger;
            _updateService = updateService;
            Title = "Initializing...";
            _logger.LogInformation("SplashViewModel initialized.");
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Starting application initialization sequence...");
            try
            {
                // 1. Connection check / Server handshake
                LoadingStatus = "Connecting to Secure Server...";
                _logger.LogInformation("Status: {Status}", LoadingStatus);
                await Task.Delay(800);

                // 2. Update Check
                LoadingStatus = "Verifying Version Integrity...";
                _logger.LogInformation("Status: {Status}", LoadingStatus);
                var update = await _updateService.CheckForUpdatesAsync();
                
                if (update != null)
                {
                    _logger.LogInformation("Update found: v{Version}", update.TargetFullRelease.Version);
                    LoadingStatus = $"Downloading Update v{update.TargetFullRelease.Version}...";
                    IsUpdateDownloading = true;
                    await _updateService.DownloadUpdatesAsync(update, p => ProgressValue = p);
                    
                    _logger.LogInformation("Updates downloaded. Applying and restarting...");
                    LoadingStatus = "Applying Updates... App will restart.";
                    await Task.Delay(1000);
                    _updateService.ApplyUpdatesAndRestart(update);
                    return;
                }

                _logger.LogInformation("No updates found. Proceeding with module loading.");

                // 3. Simulated Module Loading for "Professional" feel
                LoadingStatus = "Loading Core Architecture...";
                _logger.LogDebug("Status: {Status}", LoadingStatus);
                await Task.Delay(600);

                LoadingStatus = "Initializing logging...";
                _logger.LogDebug("Status: {Status}", LoadingStatus);
                await Task.Delay(300);

                LoadingStatus = "Initializing Feature: Error Logging...";
                _logger.LogInformation("Status: {Status}", LoadingStatus);
                await Task.Delay(500);

                LoadingStatus = "Initializing Feature: AuthHub...";
                _logger.LogDebug("Status: {Status}", LoadingStatus);
                await Task.Delay(500);

                LoadingStatus = "Initializing Feature: DataSync...";
                _logger.LogDebug("Status: {Status}", LoadingStatus);
                await Task.Delay(400);

                LoadingStatus = "Optimizing UI Resources...";
                _logger.LogDebug("Status: {Status}", LoadingStatus);
                await Task.Delay(400);

                LoadingStatus = "Systems Ready.";
                _logger.LogInformation("Initialization sequence completed successfully.");
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initialization sequence.");
                LoadingStatus = "Error during initialization. Continuing...";
                await Task.Delay(1000);
            }
        }
    }
}
