using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Mobile.Services;

namespace OCC.Mobile.ViewModels.Dashboard
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;

        public DashboardViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
            Title = "Field Operations";
        }
    }
}
