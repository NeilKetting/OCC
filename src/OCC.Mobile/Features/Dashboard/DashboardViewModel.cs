using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;

namespace OCC.Mobile.Features.Dashboard
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
