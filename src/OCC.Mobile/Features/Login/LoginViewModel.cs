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

        public Array Environments => Enum.GetValues(typeof(AppEnvironment));

        public LoginViewModel(
            INavigationService navigationService, 
            ILocalSettingsService settingsService,
            IAuthService authService)
        {
            _navigationService = navigationService;
            _settingsService = settingsService;
            _authService = authService;
            Title = "Login";

            // Load saved settings
            Username = _settingsService.Settings.LastEmail;
            RememberEmail = _settingsService.Settings.RememberEmail;
            SelectedEnvironment = _settingsService.Settings.SelectedEnvironment;
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

            try
            {
                var (success, error) = await _authService.LoginAsync(Username, Password);
                
                if (success && _authService.CurrentUser != null)
                {
                    // Save settings on success
                    if (RememberEmail)
                    {
                        _settingsService.Settings.LastEmail = Username;
                    }
                    else
                    {
                        _settingsService.Settings.LastEmail = string.Empty;
                    }
                    _settingsService.Settings.RememberEmail = RememberEmail;
                    _settingsService.Settings.SelectedEnvironment = SelectedEnvironment;
                    _settingsService.Save();

                    // Navigation based on Role
                    ErrorMessage = string.Empty;
                    
                    if (_authService.CurrentUser.UserRole == UserRole.Admin)
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
