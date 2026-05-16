using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using System;
using CommunityToolkit.Mvvm.Messaging;

namespace OCC.Mobile.Features.Shell
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ViewModelBase? _currentView;

        [ObservableProperty]
        private bool _isShellVisible;
 
        [ObservableProperty]
        private bool _isUpdateAvailable;

        [ObservableProperty]
        private string _updateVersion = string.Empty;

        [ObservableProperty]
        private string _updateReleaseNotes = string.Empty;

        [ObservableProperty]
        private bool _isDownloadingUpdate;

        [ObservableProperty]
        private double _downloadProgress;

        [ObservableProperty]
        private string _downloadStatus = "Downloading Update...";

        [ObservableProperty]
        private string _downloadSpeed = string.Empty;
        
        private UpdateCheckResult? _pendingUpdate;
 
        private readonly INavigationService _navigationService;
        private readonly IAuthService _authService;
        private readonly IUpdateService? _updateService;
        private readonly IAppInstaller? _appInstaller;
 
        public MainViewModel(
            INavigationService navigationService, 
            IAuthService authService, 
            ISignalRService signalRService,
            IUpdateService? updateService = null,
            IAppInstaller? appInstaller = null)
        {
            _navigationService = navigationService;
            _authService = authService;
            _updateService = updateService;
            _appInstaller = appInstaller;
            Title = "Orange Circle Construction";
 
            // Ensure SignalR is started if we're already authenticated
            if (!string.IsNullOrEmpty(_authService.CurrentToken))
            {
                signalRService.StartAsync().FireAndForget();
            }

            // Listen for manual update checks
            WeakReferenceMessenger.Default.Register<UpdateCheckMessage>(this, (r, m) => 
            {
                ((MainViewModel)r).CheckForUpdatesAsync(true).FireAndForget();
            });

            // Check for updates automatically
            CheckForUpdatesAsync().FireAndForget();
        }

        private async Task CheckForUpdatesAsync(bool isManual = false)
        {
            if (_updateService == null || _appInstaller == null) return;
            
            // Give the network a few seconds to initialize
            await Task.Delay(3000);
            
            try
            {
                var result = await _updateService.CheckForUpdatesAsync();
                
                if (result.IsUpdateAvailable)
                {
                    _pendingUpdate = result;
                    UpdateVersion = result.LatestVersion;
                    UpdateReleaseNotes = result.ReleaseNotes;
                    IsUpdateAvailable = true;
                }
                else if (isManual)
                {
                    await _appInstaller!.ShowToastAsync("You are up to date!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private async Task StartUpdate()
        {
            if (_pendingUpdate == null || _updateService == null || _appInstaller == null) return;

            try
            {
                IsUpdateAvailable = false;
                IsDownloadingUpdate = true;
                DownloadProgress = 0;
                DownloadStatus = "Downloading Update...";
                DownloadSpeed = "";

                var localPath = await _updateService.DownloadUpdateAsync(_pendingUpdate, p => 
                {
                    DownloadProgress = p;
                });

                if (!string.IsNullOrEmpty(localPath))
                {
                    DownloadStatus = "Launching Installer...";
                    DownloadProgress = 1.0;
                    
                    // Small delay to ensure UI updates before we lose focus
                    await Task.Delay(1000);
                    
                    await _appInstaller.InstallPackageAsync(localPath);
                }
                else
                {
                    DownloadStatus = "Download failed: Empty path";
                    await Task.Delay(3000);
                }
            }
            catch (Exception ex)
            {
                DownloadStatus = $"Error: {ex.Message}";
                await Task.Delay(5000);
                System.Diagnostics.Debug.WriteLine($"Update failed: {ex.Message}");
            }
            finally
            {
                IsDownloadingUpdate = false;
            }
        }

 
 
        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToDashboard() => _navigationService.NavigateTo<Dashboard.DashboardViewModel>();
 
        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToProjects() => _navigationService.NavigateTo<Dashboard.ActiveProjectsViewModel>();
 
        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToTasks() => _navigationService.NavigateTo<Dashboard.MyTasksViewModel>();
 
        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToHseq() => _navigationService.NavigateTo<HSEQ.HseqListViewModel>();
 
        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToProfile() => _navigationService.NavigateTo<Profile.ProfileViewModel>();
 
        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            IsShellVisible = false;
            _navigationService.NavigateTo<Login.LoginViewModel>();
        }
 
        partial void OnCurrentViewChanged(ViewModelBase? value)
        {
            // Shell is visible if we're not on Login or Register screens
            var typeName = value?.GetType().Name;
            IsShellVisible = typeName != "LoginViewModel" && typeName != "RegisterViewModel";
        }
    }
}
