using System;

namespace OCC.Mobile.Features.Notifications
{
    public class PushNotificationService : IPushNotificationService
    {
        public string? FCMToken { get; private set; }
        public event EventHandler<string>? TokenChanged;
        public event EventHandler<NotificationEventArgs>? NotificationReceived;

        public void Initialize()
        {
            // Initialization logic if needed (e.g. requesting permissions)
        }

        public void UpdateToken(string token)
        {
            if (FCMToken != token)
            {
                FCMToken = token;
                TokenChanged?.Invoke(this, token);
                System.Diagnostics.Debug.WriteLine($"[Notifications] Token updated: {token}");
                // TODO: Send token to API
            }
        }

        public void HandleNotification(string title, string body)
        {
            NotificationReceived?.Invoke(this, new NotificationEventArgs(title, body));
            System.Diagnostics.Debug.WriteLine($"[Notifications] Received: {title} - {body}");
        }
    }
}
