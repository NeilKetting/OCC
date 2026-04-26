using Android.App;
using Android.Content;
using Firebase.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OCC.Mobile.Features.Notifications;
using System;

namespace OCC.Mobile.Android.Services
{
    [Service(Name = "occ.mobile.FirebaseService", Exported = true)]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class FirebaseService : FirebaseMessagingService
    {
        public const string ChannelId = "occ_mobile_notifications";

        public FirebaseService()
        {
        }

        protected FirebaseService(IntPtr handle, global::Android.Runtime.JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }

        public override void OnMessageReceived(RemoteMessage message)
        {
            base.OnMessageReceived(message);

            string title = message.GetNotification()?.Title ?? (message.Data.ContainsKey("title") ? message.Data["title"] : "OCC Mobile Update");
            string body = message.GetNotification()?.Body ?? (message.Data.ContainsKey("message") ? message.Data["message"] : "New field update");

            ShowNotification(title, body);

            var pushService = ((App)Avalonia.Application.Current!).Services?.GetService<IPushNotificationService>();
            pushService?.HandleNotification(title, body);
        }

        public override void OnNewToken(string token)
        {
            base.OnNewToken(token);
            System.Diagnostics.Debug.WriteLine($"New FCM Token: {token}");
            
            var pushService = ((App)Avalonia.Application.Current!).Services?.GetService<IPushNotificationService>();
            pushService?.UpdateToken(token);
        }

        private void ShowNotification(string title, string body)
        {
            CreateNotificationChannel();

            var intent = new Intent(this, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.ClearTop);
            
            var flags = PendingIntentFlags.UpdateCurrent;
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                flags |= PendingIntentFlags.Immutable;
            }

            var pendingIntent = PendingIntent.GetActivity(this, 0, intent, flags);

            var builder = new AndroidX.Core.App.NotificationCompat.Builder(this, ChannelId)
                .SetSmallIcon(Resource.Drawable.icon)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetAutoCancel(true)
                .SetPriority(AndroidX.Core.App.NotificationCompat.PriorityHigh)
                .SetContentIntent(pendingIntent);

            var notificationManager = AndroidX.Core.App.NotificationManagerCompat.From(this);
            notificationManager.Notify(new Random().Next(), builder.Build());
        }

        private void CreateNotificationChannel()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                var channel = new NotificationChannel(ChannelId, "OCC Mobile Notifications", NotificationImportance.High)
                {
                    Description = "Notifications for construction field operations"
                };
                channel.EnableVibration(true);
                channel.SetShowBadge(true);

                var notificationManager = (NotificationManager)GetSystemService(Context.NotificationService)!;
                notificationManager.CreateNotificationChannel(channel);
            }
        }
    }
}
