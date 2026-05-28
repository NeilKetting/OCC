using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Models;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services
{
    public class ToastService : IToastService
    {
        public void ShowInfo(string title, string message, bool isSticky = false) => Send(title, message, ToastType.Info, isSticky);
        public void ShowSuccess(string title, string message, bool isSticky = false) => Send(title, message, ToastType.Success, isSticky);
        public void ShowWarning(string title, string message, bool isSticky = false) => Send(title, message, ToastType.Warning, isSticky);
        public void ShowError(string title, string message, bool isSticky = false) => Send(title, message, ToastType.Error, isSticky);

        private void Send(string title, string message, ToastType type, bool isSticky)
        {
            WeakReferenceMessenger.Default.Send(new ToastNotificationMessage(new ToastMessage
            {
                Title = title,
                Message = message,
                Type = type,
                IsSticky = isSticky
            }));
        }
    }
}
