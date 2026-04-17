using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

using OCC.Client.ViewModels.Core;

namespace OCC.Client.ViewModels.Shared
{
    public partial class ChangeEmailPopupViewModel : ViewModelBase
    {
        #region Events

        public event EventHandler? CloseRequested;
        public event EventHandler<string>? EmailChanged;

        #endregion

        #region Observables

        [ObservableProperty]
        private string _newEmail = string.Empty;

        #endregion

        #region Commands

        [RelayCommand]
        private void Cancel()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void ChangeEmail()
        {
            if (!string.IsNullOrWhiteSpace(NewEmail))
            {
                EmailChanged?.Invoke(this, NewEmail);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
