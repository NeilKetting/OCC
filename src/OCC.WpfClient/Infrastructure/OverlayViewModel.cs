using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Models;
using System;

namespace OCC.WpfClient.Infrastructure
{
    /// <summary>
    /// Base class for ViewModels that are shown within an overlay/drawer.
    /// Provides standardized methods for closing with a result.
    /// </summary>
    public abstract partial class OverlayViewModel : ViewModelBase
    {
        /// <summary>
        /// Event raised when the overlay wants to close.
        /// The object parameter is an optional result (e.g. the saved item).
        /// </summary>
        public event EventHandler<object?>? CloseRequested;

        /// <summary>
        /// Closes the overlay without a result.
        /// </summary>
        [RelayCommand]
        public virtual void Close()
        {
            CloseRequested?.Invoke(this, null);
        }

        /// <summary>
        /// Closes the overlay with a specific result.
        /// </summary>
        /// <param name="result">The result to return to the host.</param>
        public virtual void Close(object? result)
        {
            CloseRequested?.Invoke(this, result);
        }

        #region Extra Notifications
        protected void NotifyWarning(string title, string message)
        {
            WeakReferenceMessenger.Default.Send(new ToastNotificationMessage(new ToastMessage
            {
                Title = title,
                Message = message,
                Type = ToastType.Warning
            }));
        }

        protected void NotifyInfo(string title, string message)
        {
            WeakReferenceMessenger.Default.Send(new ToastNotificationMessage(new ToastMessage
            {
                Title = title,
                Message = message,
                Type = ToastType.Info
            }));
        }
        #endregion
    }
}
