using Android.App;
using Android.Content;
using Firebase.Messaging;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace OCC.Client.Android.Services
{
    [Service(Name = "occ.client.FirebaseService", Exported = true)]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class FirebaseService : FirebaseMessagingService
    {
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

            // Extract content
            string title = message.GetNotification()?.Title ?? (message.Data.ContainsKey("title") ? message.Data["title"] : "OCC Update");
            string body = message.GetNotification()?.Body ?? (message.Data.ContainsKey("message") ? message.Data["message"] : "New update from office");

            // Show local notification using the same logic as our AlarmReceiver
            ShowNotification(title, body);
        }

        public override void OnNewToken(string token)
        {
            base.OnNewToken(token);
            // TODO: Send this token to the API so the boss knows this device's address
            System.Diagnostics.Debug.WriteLine($"New FCM Token: {token}");
        }

        private void ShowNotification(string title, string body)
        {
            // Ensure channel exists (in case AlarmReceiver hasn't run yet)
            CreateNotificationChannel();

            var intent = new Intent(this, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.ClearTop);
            
            var flags = PendingIntentFlags.UpdateCurrent;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
            {
                flags |= PendingIntentFlags.Immutable;
            }

            var pendingIntent = PendingIntent.GetActivity(this, 0, intent, flags);

            var builder = new AndroidX.Core.App.NotificationCompat.Builder(this, AlarmReceiver.ChannelId)
                .SetSmallIcon(Resource.Drawable.occ_app_icon)
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
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(AlarmReceiver.ChannelId, "OCC Reminders", NotificationImportance.High)
                {
                    Description = "Alarms and reminders for field tasks"
                };
                channel.EnableVibration(true);
                channel.SetShowBadge(true);

                var notificationManager = (NotificationManager)GetSystemService(Context.NotificationService)!;
                notificationManager.CreateNotificationChannel(channel);
            }
        }
    }
}
