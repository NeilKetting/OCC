using Android.App;
using Android.Content;
using Firebase.Messaging;
using System;
using System.Collections.Generic;

namespace OCC.Mobile.Android.Services
{
    /*
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
    */
    public class FirebaseService 
    {

        public override void OnMessageReceived(RemoteMessage message)
        {
            base.OnMessageReceived(message);

            string title = message.GetNotification()?.Title ?? (message.Data.ContainsKey("title") ? message.Data["title"] : "OCC Mobile Update");
            string body = message.GetNotification()?.Body ?? (message.Data.ContainsKey("message") ? message.Data["message"] : "New field update");

            ShowNotification(title, body);
        }

        public override void OnNewToken(string token)
        {
            base.OnNewToken(token);
            System.Diagnostics.Debug.WriteLine($"New FCM Token: {token}");
        }

        private void ShowNotification(string title, string body)
        {
            CreateNotificationChannel();

            var intent = new Intent(this, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.ClearTop);
            
            var flags = PendingIntentFlags.UpdateCurrent;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
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
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
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
