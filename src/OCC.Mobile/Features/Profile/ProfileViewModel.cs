using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.Profile
{
    public partial class ProfileViewModel : ViewModelBase
    {
        [ObservableProperty]
        private User? _currentUser;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        private readonly IUpdateService? _updateService;

        public ProfileViewModel(IAuthService authService, INavigationService navigationService, IUpdateService? updateService = null)
        {
            _authService = authService;
            _navigationService = navigationService;
            _updateService = updateService;
            CurrentUser = _authService.CurrentUser;
            AppVersion = _updateService?.CurrentVersion ?? "1.0.0";
            Title = "My Profile";
        }

        [RelayCommand]
        private void CheckForUpdates()
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send("CheckForUpdates");
        }

        [RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            _navigationService.NavigateTo<Login.LoginViewModel>();
        }
    }
}
