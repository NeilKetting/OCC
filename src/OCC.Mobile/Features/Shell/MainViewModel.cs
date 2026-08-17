using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using System;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;

namespace OCC.Mobile.Features.Shell
{
    /// <summary>
    /// The MainViewModel for the Mobile Client. Handles shell visibility, mobile app updates,
    /// and routing commands between the dashboard, active projects, tasks, profile, and login.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        #region Observables & Properties

        // --- View & Navigation Observables ---
        
        // Tracks the current screen/view displayed in the mobile client
        [ObservableProperty]
        private ViewModelBase? _currentView;

        // Controlled by whether the user is on Login/Register or an authenticated dashboard screen
        [ObservableProperty]
        private bool _isShellVisible;
 
        // --- Over-The-Air Update Observables ---
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

        #endregion

        #region Private Fields & Services
        
        // Reference containing metadata for the pending mobile app package
        private UpdateCheckResult? _pendingUpdate;

        // Guard to ensure update check only runs once per process lifetime
        private static bool _hasCheckedForUpdates;
 
        // --- Dependency Injected Services ---
        private readonly INavigationService _navigationService;
        private readonly IAuthService _authService;
        private readonly IUpdateService? _updateService;
        private readonly IAppInstaller? _appInstaller;

        #endregion

        #region Constructors
 
        /// <summary>
        /// Initializes the mobile main shell. Triggers initial app update checks and registers message hooks.
        /// </summary>
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

            // Listen for manual update check events (e.g. from a settings page check button)
            WeakReferenceMessenger.Default.Register<UpdateCheckMessage>(this, (r, m) => 
            {
                ((MainViewModel)r).CheckForUpdatesAsync(true).FireAndForget();
            });

            // Check for updates automatically in the background on startup
            CheckForUpdatesAsync().FireAndForget();

            // Check and upload any pending crashes asynchronously after startup
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000); // Give the app a few seconds to settle
                await OCC.Mobile.Infrastructure.CrashDetector.UploadPendingCrashesAsync(App.Services);
            });
        }

        #endregion

        #region Methods & Update Logic
 
        /// <summary>
        /// Contacts the update repository to see if a newer version of the mobile package is available.
        /// </summary>
        private async Task CheckForUpdatesAsync(bool isManual = false)
        {
            if (_updateService == null || _appInstaller == null) return;

            // On automatic startup check, skip if we've already checked this process lifetime.
            // This prevents an infinite loop: install update → app restarts → detects same update again.
            if (!isManual)
            {
                if (_hasCheckedForUpdates) return;
                _hasCheckedForUpdates = true;
            }
            else
            {
                // Manual check: reset so user can force a fresh check
                _hasCheckedForUpdates = false;
            }
            
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

        #endregion

        #region Commands

        /// <summary>
        /// Downloads the mobile application package OTA and invokes the platform package installer.
        /// </summary>
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
                    
                    // Small delay to ensure UI updates before focus switch
                    await Task.Delay(1000);
                    
                    var installerLaunched = await _appInstaller.InstallPackageAsync(localPath);
                    if (!installerLaunched)
                    {
                        DownloadStatus = "Permission needed in Settings. Tap Update again after allowing.";
                        await Task.Delay(3500);
                        IsUpdateAvailable = true;
                    }
                }
                else
                {
                    DownloadStatus = "Download failed: Empty path";
                    await Task.Delay(3000);
                    IsUpdateAvailable = true;
                }
            }
            catch (Exception ex)
            {
                DownloadStatus = $"Error: {ex.Message}";
                await Task.Delay(4000);
                IsUpdateAvailable = true;
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
        private void NavigateToTasks() => _navigationService.NavigateTo<Tasks.RedesignTasksViewModel>();
 
        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToHseq() => _navigationService.NavigateTo<HSEQ.HseqListViewModel>();
 
        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToProfile() => _navigationService.NavigateTo<Profile.ProfileViewModel>();
 
        /// <summary>
        /// Logs the user out of the mobile application session.
        /// </summary>
        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            IsShellVisible = false;
            _navigationService.NavigateTo<Login.LoginViewModel>();
        }

        #endregion

        #region Event Handlers
 
        /// <summary>
        /// Automatically manages shell navigation layout visibility based on active view type name.
        /// </summary>
        partial void OnCurrentViewChanged(ViewModelBase? value)
        {
            // Shell is visible if we're not on Login or Register screens
            var typeName = value?.GetType().Name;
            IsShellVisible = typeName != "LoginViewModel" && typeName != "RegisterViewModel";

            OnPropertyChanged(nameof(IsDashboardActive));
            OnPropertyChanged(nameof(IsProjectsActive));
            OnPropertyChanged(nameof(IsTasksActive));
            OnPropertyChanged(nameof(IsHseqActive));
            OnPropertyChanged(nameof(IsProfileActive));
        }

        public bool IsDashboardActive => CurrentView is Dashboard.DashboardViewModel;
        public bool IsProjectsActive => CurrentView is Dashboard.ActiveProjectsViewModel;
        public bool IsTasksActive => CurrentView is Tasks.RedesignTasksViewModel;
        public bool IsHseqActive => CurrentView is HSEQ.HseqListViewModel;
        public bool IsProfileActive => CurrentView is Profile.ProfileViewModel;

        #endregion
    }
}
