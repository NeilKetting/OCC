using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.Shell.ViewModels
{
    public partial class ShellViewModel : ViewModelBase
    {
        [ObservableProperty]
        private INavigationService _navigation;

        public ShellViewModel(INavigationService navigation)
        {
            _navigation = navigation;
            Title = "Orange Circle Construction - ERP";
        }
    }
}
