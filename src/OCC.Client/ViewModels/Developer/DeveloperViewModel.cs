using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Infrastructure; // For ConnectionSettings
using OCC.Client.ViewModels.Core;
using System;
using System.Threading.Tasks;

namespace OCC.Client.ViewModels.Developer
{
    public partial class DeveloperViewModel : ViewModelBase
    {
        private readonly ILogger<DeveloperViewModel> _logger;
        private readonly SignalRNotificationService _signalRService;
        private readonly IDialogService _dialogService;
        private readonly ILogUploadService _logService;

        [ObservableProperty]
        private string _broadcastMessage = string.Empty;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<OCC.Shared.Models.LogUploadRequest> _logs = new();

        public DeveloperViewModel(
            ILogger<DeveloperViewModel> logger,
            SignalRNotificationService signalRService,
            IDialogService dialogService,
            ILogUploadService logService)
        {
            _logger = logger;
            _signalRService = signalRService;
            _dialogService = dialogService;
            _logService = logService;
        }

        public DeveloperViewModel()
        {
            _logger = null!;
            _signalRService = null!;
            _dialogService = null!;
            _logService = null!;
        }

        [RelayCommand]
        public async Task LoadLogs()
        {
            try
            {
                IsBusy = true;
                var logs = await _logService.GetLogsAsync();
                Logs = new System.Collections.ObjectModel.ObservableCollection<OCC.Shared.Models.LogUploadRequest>(logs);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to load logs: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task DeleteLog(OCC.Shared.Models.LogUploadRequest log)
        {
            try
            {
                if (await _dialogService.ShowConfirmationAsync("Delete Log", "Are you sure you want to delete this log?"))
                {
                     await _logService.DeleteLogAsync(log.Id);
                     Logs.Remove(log);
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to delete log: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task DownloadLog(OCC.Shared.Models.LogUploadRequest log)
        {
            try
            {
                // Simple download: Trigger browser or save dialog? 
                // Since this is desktop, we should probably show SaveFileDialog or just open in browser.
                // Opening in browser is easiest if we had a direct link, but endpoint returns a file stream.
                // Let's us the launcher to open the URL directly which will trigger browser download.
                
                var baseUrl = ConnectionSettings.Instance.ApiBaseUrl.TrimEnd('/');
                var url = $"{baseUrl}/api/Logs/download/{log.Id}";
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                 await _dialogService.ShowAlertAsync("Error", $"Failed to download log: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task SendBroadcast()
        {
            if (string.IsNullOrWhiteSpace(BroadcastMessage)) return;

            try
            {
                IsBusy = true;
                // We'll need to implement this in SignalRNotificationService
                await _signalRService.SendBroadcastMessageAsync("System Admin", BroadcastMessage);
                
                await _dialogService.ShowAlertAsync("Broadcast Sent", "Message has been sent to all active users.");
                BroadcastMessage = string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send broadcast");
                await _dialogService.ShowAlertAsync("Error", $"Failed to send broadcast: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task SimulateApiUpdate()
        {
            BroadcastMessage = "API is updating. Please save your work and log out. System will be offline in 5 minutes.";
            await SendBroadcast();
        }
    }
}
