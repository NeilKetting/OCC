using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Models;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services
{
    public class ToastService : IToastService
    {
        public void ShowInfo(string title, string message) => Send(title, message, ToastType.Info);
        public void ShowSuccess(string title, string message) => Send(title, message, ToastType.Success);
        public void ShowWarning(string title, string message) => Send(title, message, ToastType.Warning);
        public void ShowError(string title, string message) => Send(title, message, ToastType.Error);

        private void Send(string title, string message, ToastType type)
        {
            WeakReferenceMessenger.Default.Send(new ToastNotificationMessage(new ToastMessage
            {
                Title = title,
                Message = message,
                Type = type
            }));
        }
    }
}
