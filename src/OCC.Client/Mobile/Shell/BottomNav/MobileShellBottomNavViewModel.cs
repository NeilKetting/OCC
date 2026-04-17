using OCC.Client.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using OCC.Client.Mobile.Features.Dashboard;
using OCC.Client.Mobile.Features.RollCall;

namespace OCC.Client.Mobile.Shell.BottomNav
{
    public partial class MobileShellBottomNavViewModel : MobileShellViewModelBase
    {
        public MobileShellBottomNavViewModel(
            SiteManagerDashboardViewModel dashboardViewModel,
            MobileRollCallViewModel rollCallViewModel)
            : base(dashboardViewModel, rollCallViewModel)
        {
        }
    }
}
