using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Mobile.ViewModels;

namespace OCC.Mobile.Features.Shell
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ViewModelBase? _currentView;

        public MainViewModel()
        {
            Title = "Orange Circle Construction";
        }
    }
}
