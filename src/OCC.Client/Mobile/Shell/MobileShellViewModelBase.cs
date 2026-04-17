using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.ViewModels.Core;
using System;
using OCC.Client.Mobile.Features.Dashboard;
using OCC.Client.Mobile.Features.RollCall;

namespace OCC.Client.Mobile.Shell
{
    public partial class MobileShellViewModelBase : ViewModelBase
    {
        [ObservableProperty]
        private ViewModelBase _currentView;

        private readonly SiteManagerDashboardViewModel _dashboardViewModel;
        private readonly MobileRollCallViewModel _rollCallViewModel;

        public MobileShellViewModelBase(
            SiteManagerDashboardViewModel dashboardViewModel,
            MobileRollCallViewModel rollCallViewModel)
        {
            _dashboardViewModel = dashboardViewModel;
            _rollCallViewModel = rollCallViewModel;
            _currentView = _dashboardViewModel;
        }

        [RelayCommand]
        private void NavigateToDashboard() => CurrentView = _dashboardViewModel;

        [RelayCommand]
        private void NavigateToRollCall() => CurrentView = _rollCallViewModel;
    }
}
