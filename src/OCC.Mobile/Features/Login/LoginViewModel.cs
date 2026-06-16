using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Features.Dashboard;
using OCC.Mobile.Features.AdminDashboard;
using OCC.Mobile.Services;
using OCC.Shared.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace OCC.Mobile.Features.Login
{
    public partial class LoginViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly ILocalSettingsService _settingsService;
        private readonly IAuthService _authService;
        private readonly ISignalRService _signalRService;
        private readonly Features.Notifications.IPushNotificationService _pushNotificationService;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _pushStatus = "Initializing...";

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _rememberEmail;

        [ObservableProperty]
        private AppEnvironment _selectedEnvironment;

        [ObservableProperty]
        private string? _customLocalUrl;
        
        [ObservableProperty]
        private int _activeProjectsCount;

        [ObservableProperty]
        private int _tasksTodayCount;

        [ObservableProperty]
        private int _liveSitesCount;

        [ObservableProperty]
        private int _teamMembersCount;

        private readonly System.Threading.SemaphoreSlim _statsSemaphore = new(1, 1);
        
        public string AppVersion => App.AppVersion;

        public Array Environments => Enum.GetValues(typeof(AppEnvironment));

        public LoginViewModel(
            INavigationService navigationService, 
            ILocalSettingsService settingsService,
            IAuthService authService,
            ISignalRService signalRService,
            Features.Notifications.IPushNotificationService pushNotificationService)
        {
            _navigationService = navigationService;
            _settingsService = settingsService;
            _authService = authService;
            _signalRService = signalRService;
            _pushNotificationService = pushNotificationService;
            Title = "Login";

            // Load saved settings
            ActiveProjectsCount = _settingsService.Settings.CachedActiveProjects;
            TasksTodayCount = _settingsService.Settings.CachedTasksToday;
            LiveSitesCount = _settingsService.Settings.CachedLiveSites;
            TeamMembersCount = _settingsService.Settings.CachedTeamMembers;

            Username = _settingsService.Settings.LastEmail;
            RememberEmail = _settingsService.Settings.RememberEmail;
            SelectedEnvironment = _settingsService.Settings.SelectedEnvironment;
            CustomLocalUrl = _settingsService.Settings.CustomLocalUrl;
            
            PushStatus = _pushNotificationService.Status;
            
            if (_pushNotificationService is Features.Notifications.PushNotificationService pns)
            {
                pns.StatusChanged += (s, e) => PushStatus = e;
            }

            // Fallback for first-time use to automatically pick up the PC's local IP
            if (string.IsNullOrEmpty(CustomLocalUrl))
            {
                CustomLocalUrl = $"http://{GetLocalIPAddress()}:5237";
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var ipStr = ip.ToString();
                        // Prefer standard local subnets
                        if (ipStr.StartsWith("192.168.") || ipStr.StartsWith("10.") || ipStr.StartsWith("172."))
                        {
                            return ipStr;
                        }
                    }
                }
                
                var anyIp = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                if (anyIp != null) return anyIp.ToString();
            }
            catch
            {
                // Ignore and fallback to localhost
            }
            return "127.0.0.1";
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both username and password.";
                return;
            }

            IsBusy = true;
            BusyText = "Connecting to API...";

            // Save environment and URL even if login fails, so user doesn't have to re-type IP
            _settingsService.Settings.SelectedEnvironment = SelectedEnvironment;
            _settingsService.Settings.CustomLocalUrl = CustomLocalUrl;
            _settingsService.Save();

            try
            {
                var (success, error) = await _authService.LoginAsync(Username, Password);
                
                if (success && _authService.CurrentUser != null)
                {
                    // Save additional settings on success
                    if (RememberEmail)
                    {
                        _settingsService.Settings.LastEmail = Username;
                    }
                    else
                    {
                        _settingsService.Settings.LastEmail = string.Empty;
                    }
                    _settingsService.Settings.RememberEmail = RememberEmail;
                    _settingsService.Save();

                    // Start SignalR
                    _signalRService.StartAsync().FireAndForget();

                    // Sync Push Token
                    _pushNotificationService.RegisterWithApiAsync().FireAndForget();

                    // Navigation based on Role
                    ErrorMessage = string.Empty;
                    
                    // Always land on Dashboard (Overview) for now to ensure visibility of Push Status
                    _navigationService.NavigateTo<Dashboard.DashboardViewModel>();
                }
                else
                {
                    ErrorMessage = error ?? "Login failed.";
                }
            }
            catch (System.Exception ex)
            {
                ErrorMessage = "Login failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void NavigateToRegister()
        {
            _navigationService.NavigateTo<Register.RegisterViewModel>();
        }

        partial void OnSelectedEnvironmentChanged(AppEnvironment value)
        {
            _settingsService.Settings.SelectedEnvironment = value;
            LoadPublicStatsAsync().FireAndForget();
        }

        partial void OnCustomLocalUrlChanged(string? value)
        {
            _settingsService.Settings.CustomLocalUrl = value;
            LoadPublicStatsAsync().FireAndForget();
        }

        private async Task LoadPublicStatsAsync()
        {
            if (!await _statsSemaphore.WaitAsync(0)) return;
            try
            {
                var baseUrl = _authService.GetBaseUrl();
                var url = $"{baseUrl}api/System/public-stats";
                
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Remove("X-Environment");
                client.DefaultRequestHeaders.Add("X-Environment", SelectedEnvironment.ToString());
                
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var stats = await response.Content.ReadFromJsonAsync<PublicStatsDto>();
                    if (stats != null)
                    {
                        ActiveProjectsCount = stats.ActiveProjectsCount;
                        TasksTodayCount = stats.TasksTodayCount;
                        LiveSitesCount = stats.LiveSitesCount;
                        TeamMembersCount = stats.TeamMembersCount;

                        // Cache them
                        _settingsService.Settings.CachedActiveProjects = stats.ActiveProjectsCount;
                        _settingsService.Settings.CachedTasksToday = stats.TasksTodayCount;
                        _settingsService.Settings.CachedLiveSites = stats.LiveSitesCount;
                        _settingsService.Settings.CachedTeamMembers = stats.TeamMembersCount;
                        _settingsService.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load public stats: {ex.Message}");
            }
            finally
            {
                _statsSemaphore.Release();
            }
        }

        private class PublicStatsDto
        {
            public int ActiveProjectsCount { get; set; }
            public int TasksTodayCount { get; set; }
            public int LiveSitesCount { get; set; }
            public int TeamMembersCount { get; set; }
        }
    }
}
