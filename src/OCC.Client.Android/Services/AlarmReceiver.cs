using Android.App;
using Android.Content;
using AndroidX.Core.App;
using System;
using System.Runtime.Versioning;

namespace OCC.Client.Android.Services
{
    [BroadcastReceiver(Name = "occ.client.AlarmReceiver", Enabled = true, Exported = true)]
    [IntentFilter(new[] { "OCC_REMINDER_ALARM" })]
    public class AlarmReceiver : BroadcastReceiver
    {
        public const string ChannelId = "occ_reminders_channel";
        public const int NotificationId = 1001;

        public AlarmReceiver()
        {
        }

        protected AlarmReceiver(IntPtr handle, global::Android.Runtime.JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context == null || intent == null) return;

            string title = intent.GetStringExtra("title") ?? "OCC Reminder";
            string message = intent.GetStringExtra("message") ?? "Task due now";

            CreateNotificationChannel(context);

            var notificationIntent = new Intent(context, typeof(MainActivity));
            notificationIntent.AddFlags(ActivityFlags.ClearTop);
            var flags = PendingIntentFlags.UpdateCurrent;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
            {
                flags |= PendingIntentFlags.Immutable;
            }
            
            var pendingIntent = PendingIntent.GetActivity(context, 0, notificationIntent, flags);

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetSmallIcon(Resource.Drawable.occ_app_icon)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetAutoCancel(true)
                .SetDefaults((int)NotificationDefaults.All)
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetCategory(NotificationCompat.CategoryAlarm)
                .SetContentIntent(pendingIntent);

            var notificationManager = NotificationManagerCompat.From(context);
            notificationManager.Notify(new Random().Next(), builder.Build());
        }

        private void CreateNotificationChannel(Context context)
        {
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(ChannelId, "OCC Reminders", NotificationImportance.High)
                {
                    Description = "Alarms and reminders for field tasks"
                };
                channel.EnableVibration(true);
                channel.SetShowBadge(true);

                var notificationManager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
                notificationManager.CreateNotificationChannel(channel);
            }
        }
    }
}
