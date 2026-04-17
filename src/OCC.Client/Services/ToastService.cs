using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Models;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Messages;
using System;

namespace OCC.Client.Services
{
    public class ToastService : IToastService
    {
        public void ShowSuccess(string title, string message) => Send(title, message, ToastType.Success);
        public void ShowError(string title, string message) => Send(title, message, ToastType.Error);
        public void ShowInfo(string title, string message) => Send(title, message, ToastType.Info);
        public void ShowWarning(string title, string message) => Send(title, message, ToastType.Warning);

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
