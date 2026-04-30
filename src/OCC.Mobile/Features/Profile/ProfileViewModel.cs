using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.Profile
{
    public partial class ProfileViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
 
        [ObservableProperty]
        private User? _currentUser;

        public ProfileViewModel(IAuthService authService, INavigationService navigationService)
        {
            _authService = authService;
            _navigationService = navigationService;
            CurrentUser = _authService.CurrentUser;
            Title = "My Profile";
        }

        [RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            _navigationService.NavigateTo<Login.LoginViewModel>();
        }
    }
}
