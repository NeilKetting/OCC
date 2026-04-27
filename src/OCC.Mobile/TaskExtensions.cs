using System;
using System.Threading.Tasks;

namespace OCC.Mobile
{
    public static class TaskExtensions
    {
        public static void FireAndForget(this Task task, Action<Exception>? onException = null)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted && onException != null)
                {
                    onException(t.Exception?.Flatten().InnerException ?? t.Exception!);
                }
                else if (t.IsFaulted)
                {
                    System.Diagnostics.Debug.WriteLine($"Task failed: {t.Exception?.Message}");
                }
            }, TaskScheduler.Default);
        }
    }
}
