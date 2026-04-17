using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace OCC.Client.ViewModels.Core
{
    public partial class AccessDeniedViewModel : ViewModelBase
    {
        public event EventHandler? CloseRequested;

        [ObservableProperty]
        private string _title = "Insufficient Permissions";

        [ObservableProperty]
        private string _message = "Your account does not have the required permissions to view this content.\nContact your administrator if access is required.";

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
