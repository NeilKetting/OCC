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

            // Check for updates
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
                    // For now, let's just trigger it. 
                    // In a real premium app, we'd show a "New version available" overlay.
                    // But for a client tablet, keeping it simple "Update now?" is often better.
                    
                    // We'll use a simple background download and then prompt.
                    // Note: You might want to add a UI prompt here.
                    var localPath = await _updateService.DownloadUpdateAsync(result, p => 
                    {
                        // Progress reporting if we had a progress bar
                        System.Diagnostics.Debug.WriteLine($"Update Download: {p:P0}");
                    });

                    if (!string.IsNullOrEmpty(localPath))
                    {
                        await _appInstaller.InstallPackageAsync(localPath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
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
