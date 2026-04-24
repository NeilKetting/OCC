using Android.App;
using Android.Content;
using Android.OS;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Client.Android.Services
{
    public class AndroidNotificationService : INotificationService
    {
        private readonly Context _context;

        public AndroidNotificationService(Context context)
        {
            _context = context;
        }

        public event EventHandler<OCC.Shared.Models.Notification>? NotificationReceived;

        public Task ClearAllAsync() => Task.CompletedTask;

        public Task DismissAsync(NotificationDismissal dismissal) => Task.CompletedTask;

        public Task<IEnumerable<Guid>> GetDismissedIdsAsync() => Task.FromResult<IEnumerable<Guid>>(new List<Guid>());

        public Task<IEnumerable<OCC.Shared.Models.Notification>> GetNotificationsAsync() => Task.FromResult<IEnumerable<OCC.Shared.Models.Notification>>(new List<OCC.Shared.Models.Notification>());

        public Task MarkAsReadAsync(Guid notificationId) => Task.CompletedTask;

        public Task SendReminderAsync(string title, string message, string? action = null)
        {
            // Immediate notification logic could go here
            return Task.CompletedTask;
        }

        public Task ScheduleAlarmAsync(Guid id, string title, string message, DateTime triggerTime)
        {
            var intent = new Intent(_context, typeof(AlarmReceiver));
            intent.SetAction("OCC_REMINDER_ALARM");
            intent.PutExtra("id", id.ToString());
            intent.PutExtra("title", title);
            intent.PutExtra("message", message);

            var pendingIntent = PendingIntent.GetBroadcast(_context, id.GetHashCode(), intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var alarmManager = (AlarmManager)_context.GetSystemService(Context.AlarmService)!;

            long triggerAtMillis = (long)(triggerTime.ToUniversalTime() - DateTimeOffset.UnixEpoch.UtcDateTime).TotalMilliseconds;

            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
            {
                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            }
            else
            {
                alarmManager.SetExact(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            }

            return Task.CompletedTask;
        }
    }
}
