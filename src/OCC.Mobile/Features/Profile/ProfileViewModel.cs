using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;

using CommunityToolkit.Mvvm.Messaging;

namespace OCC.Mobile.Features.Profile
{
    public partial class ProfileViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly IUpdateService? _updateService;

        [ObservableProperty]
        private User? _currentUser;

        [ObservableProperty]
        private string _appVersion = string.Empty;

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
            WeakReferenceMessenger.Default.Send(UpdateCheckMessage.Instance);
        }

        [RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            _navigationService.NavigateTo<Login.LoginViewModel>();
        }
    }
}
