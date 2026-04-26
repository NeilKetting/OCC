using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.Mobile.ViewModels
{
    public abstract partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _busyText = "Loading...";
    }
}
