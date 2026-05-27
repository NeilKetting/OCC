using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using OCC.WpfClient.Models;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services
{
    public class ShellTimingService : IShellTimingService
    {
        public async Task FadeOutToastAsync(ToastMessage toast, Action<ToastMessage> removeToast, CancellationToken cancellationToken)
        {
            // Give the user time to read the toast before fading it away.
            await Task.Delay(5000, cancellationToken);

            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(50, cancellationToken);
                Application.Current.Dispatcher.Invoke(() => toast.Opacity -= 0.1);
            }

            Application.Current.Dispatcher.Invoke(() => removeToast(toast));
        }

        public async Task ResetStatusAsync(string statusMessage, Func<string> getCurrentStatus, Action<string> setStatus, CancellationToken cancellationToken)
        {
            // Only reset the status if nothing newer replaced it.
            await Task.Delay(10000, cancellationToken);

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (getCurrentStatus() == statusMessage)
                {
                    setStatus("Ready");
                }
            });
        }

        public async Task HideImportProgressAsync(Action hideProgress, CancellationToken cancellationToken)
        {
            // Keep the completed progress visible briefly so the user sees it finished.
            await Task.Delay(3000, cancellationToken);
            Application.Current.Dispatcher.Invoke(hideProgress);
        }
    }
}
