using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using System;

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
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<string>(this, (r, m) => 
            {
                if (m == "CheckForUpdates") CheckForUpdatesAsync().FireAndForget();
            });

            // Check for updates automatically
            CheckForUpdatesAsync().FireAndForget();
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_updateService == null || _appInstaller == null) return;

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
                IsUpdateAvailable = false; // Hide the prompt
                IsDownloadingUpdate = true;
                DownloadProgress = 0;

                var localPath = await _updateService.DownloadUpdateAsync(_pendingUpdate, p => 
                {
                    DownloadProgress = p;
                });

                if (!string.IsNullOrEmpty(localPath))
                {
                    await _appInstaller.InstallPackageAsync(localPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update failed: {ex.Message}");
            }
            finally
            {
                IsDownloadingUpdate = false;
            }
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void DismissUpdate()
        {
            IsUpdateAvailable = false;
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
