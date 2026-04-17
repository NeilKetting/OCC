using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Models;

namespace OCC.WpfClient.Infrastructure
{
    /// <summary>
    /// Base class for all ViewModels in the WPF application.
    /// Provides common observable properties like IsBusy and Title.
    /// </summary>
    public abstract partial class ViewModelBase : ObservableValidator
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _busyText = "Please wait...";

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _isActiveHub;

        protected void NotifySuccess(string title, string message)
        {
            WeakReferenceMessenger.Default.Send(new ToastNotificationMessage(new ToastMessage
            {
                Title = title,
                Message = message,
                Type = ToastType.Success
            }));
        }

        protected void NotifyError(string title, string message)
        {
            WeakReferenceMessenger.Default.Send(new ToastNotificationMessage(new ToastMessage
            {
                Title = title,
                Message = message,
                Type = ToastType.Error
            }));
        }

        protected void UpdateStatus(string message)
        {
            WeakReferenceMessenger.Default.Send(new StatusUpdateMessage(message));
        }
    }
}
