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
 
        public MainViewModel(INavigationService navigationService, IAuthService authService, ISignalRService signalRService)
        {
            _navigationService = navigationService;
            _authService = authService;
            Title = "Orange Circle Construction";
 
            // Ensure SignalR is started if we're already authenticated
            if (!string.IsNullOrEmpty(_authService.CurrentToken))
            {
                signalRService.StartAsync().FireAndForget();
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
