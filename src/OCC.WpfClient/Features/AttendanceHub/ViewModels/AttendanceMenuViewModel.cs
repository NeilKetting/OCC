using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    public partial class AttendanceMenuViewModel : ViewModelBase
    {
        private readonly IPermissionService _permissionService;

        [ObservableProperty]
        private string _activeTab = "Dashboard";

        public AttendanceMenuViewModel(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        public bool CanAccessWages => _permissionService.CanAccess(NavigationRoutes.Feature_WagesJhb) || _permissionService.CanAccess(NavigationRoutes.Feature_WagesCpt);

        [RelayCommand]
        private void SetActiveTab(string tab) => ActiveTab = tab;
    }
}
