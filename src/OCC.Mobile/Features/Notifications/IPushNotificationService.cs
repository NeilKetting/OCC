using System;
using System.Threading.Tasks;

namespace OCC.Mobile.Features.Notifications
{
    public interface IPushNotificationService
    {
        string? FCMToken { get; }
        event EventHandler<string>? TokenChanged;
        event EventHandler<NotificationEventArgs>? NotificationReceived;
        
        void Initialize();
        void UpdateToken(string token);
        Task RegisterWithApiAsync();
        void HandleNotification(string title, string body);
    }

    public class NotificationEventArgs : EventArgs
    {
        public string Title { get; }
        public string Body { get; }

        public NotificationEventArgs(string title, string body)
        {
            Title = title;
            Body = body;
        }
    }
}
