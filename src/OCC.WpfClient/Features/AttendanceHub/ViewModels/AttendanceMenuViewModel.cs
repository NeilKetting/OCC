using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    public partial class AttendanceMenuViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _activeTab = "Dashboard";

        [RelayCommand]
        private void SetActiveTab(string tab) => ActiveTab = tab;
    }
}
