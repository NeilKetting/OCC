using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels.Dashboard;
using OCC.Mobile.Services;
using System;
using System.Threading.Tasks;

namespace OCC.Mobile.ViewModels.Login
{
    public partial class LoginViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly ILocalSettingsService _settingsService;

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

        public LoginViewModel(INavigationService navigationService, ILocalSettingsService settingsService)
        {
            _navigationService = navigationService;
            _settingsService = settingsService;
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
            BusyText = "Authenticating...";

            try
            {
                // Placeholder for actual login logic
                await Task.Delay(1500); 

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

                // Navigation to Dashboard
                ErrorMessage = string.Empty;
                _navigationService.NavigateTo<DashboardViewModel>();
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
