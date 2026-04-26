using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.Mobile.ViewModels
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
