using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using OCC.Client.Mobile.Features.Dashboard;
using OCC.Client.Mobile.Features.RollCall;
using OCC.Client.Mobile.Shell.BottomNav;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Mobile.Shell
{
    public partial class MobileHubViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ViewModelBase _currentView = null!;

        private readonly SiteManagerDashboardViewModel _dashboardViewModel;
        private readonly MobileRollCallViewModel _rollCallViewModel;

        public MobileHubViewModel(
            SiteManagerDashboardViewModel dashboardViewModel,
            MobileRollCallViewModel rollCallViewModel)
        {
            _dashboardViewModel = dashboardViewModel;
            _rollCallViewModel = rollCallViewModel;

            // Default directly to Bottom Navigation
            CurrentView = new MobileShellBottomNavViewModel(_dashboardViewModel, _rollCallViewModel);
        }
    }
}
