using System;
using System.Threading;
using System.Threading.Tasks;
using OCC.WpfClient.Models;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IShellTimingService
    {
        Task FadeOutToastAsync(ToastMessage toast, Action<ToastMessage> removeToast, CancellationToken cancellationToken);
        Task ResetStatusAsync(string statusMessage, Func<string> getCurrentStatus, Action<string> setStatus, CancellationToken cancellationToken);
        Task HideImportProgressAsync(Action hideProgress, CancellationToken cancellationToken);
    }
}
