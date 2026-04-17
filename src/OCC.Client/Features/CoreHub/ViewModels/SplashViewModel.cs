using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using System;
using System.Threading.Tasks;

namespace OCC.Client.Features.CoreHub.ViewModels
{
    public partial class SplashViewModel : ViewModelBase
    {
        private readonly IUpdateService _updateService;
        private readonly Action _onCompleted;

        [ObservableProperty]
        private string _statusText = "Checking for updates...";

        [ObservableProperty]
        private bool _isChecking = true;

        [ObservableProperty]
        private int _progress;

        [ObservableProperty]
        private string _currentVersion;

        public SplashViewModel(IUpdateService updateService, Action onCompleted)
        {
            _updateService = updateService;
            _onCompleted = onCompleted;
            _currentVersion = $"v{updateService.CurrentVersion}";

            _ = CheckUpdates();
        }

        private async Task CheckUpdates()
        {
            try
            {
                // 1. Initial delay for UX (prevent flicker if too fast)
                await Task.Delay(1000);

                // 2. Wrap update check in a timeout task
                var checkTask = _updateService.CheckForUpdatesAsync();
                var timeoutTask = Task.Delay(40000); // 40 second fail-safe

                var completedTask = await Task.WhenAny(checkTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    // Timeout occurred
                    System.Diagnostics.Debug.WriteLine("[Splash] Update check timed out.");
                    StatusText = "Update check delayed... Continuing";
                    await Task.Delay(1000);
                    _onCompleted?.Invoke();
                    return;
                }

                var updateInfo = await checkTask; // Get the actual result
                
                if (updateInfo != null)
                {
                    StatusText = "Update found! Downloading...";
                    IsChecking = false; 
                    
                    await _updateService.DownloadUpdatesAsync(updateInfo, (p) => 
                    {
                        Progress = p;
                    });

                    StatusText = "Installing update...";
                    _updateService.ApplyUpdatesAndExit(updateInfo);
                }
                else
                {
                    StatusText = "App is ready";
                    await Task.Delay(500); 
                    _onCompleted?.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Splash] Update check failed: {ex.Message}");
                StatusText = "Continuing...";
                await Task.Delay(1000);
                _onCompleted?.Invoke();
            }
        }
    }
}
