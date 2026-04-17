using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Mobile.Features.RollCall
{
    public partial class MobileDashboardViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _title = "Site Manager Dashboard";

        [ObservableProperty]
        private int _todayTasksCount = 5;

        [ObservableProperty]
        private int _overdueTasksCount = 2;

        public MobileDashboardViewModel()
        {
        }
    }
}
