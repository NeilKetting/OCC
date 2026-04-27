using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Features.Dashboard;
using OCC.Mobile.Features.AdminDashboard;
using OCC.Mobile.Services;
using OCC.Shared.Models;
using System;
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
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _rememberEmail;

        [ObservableProperty]
        private AppEnvironment _selectedEnvironment;

        [ObservableProperty]
        private string? _customLocalUrl;

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
            Username = _settingsService.Settings.LastEmail;
            RememberEmail = _settingsService.Settings.RememberEmail;
            SelectedEnvironment = _settingsService.Settings.SelectedEnvironment;
            CustomLocalUrl = _settingsService.Settings.CustomLocalUrl;
            
            // Fallback for first-time use on this IP
            if (string.IsNullOrEmpty(CustomLocalUrl))
            {
                CustomLocalUrl = "http://192.168.0.191:5237";
            }
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
                    
                    if (_authService.CurrentUser.UserRole == UserRole.Admin || 
                        _authService.CurrentUser.UserRole == UserRole.SiteManager)
                    {
                        _navigationService.NavigateTo<AdminDashboardViewModel>();
                    }
                    else
                    {
                        _navigationService.NavigateTo<DashboardViewModel>();
                    }
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
    }
}
