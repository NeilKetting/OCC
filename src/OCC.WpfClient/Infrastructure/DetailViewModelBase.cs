using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Infrastructure.Exceptions;
using OCC.WpfClient.Services.Interfaces;
using System.Diagnostics;
using System;
using System.Threading.Tasks;

namespace OCC.WpfClient.Infrastructure
{
    public abstract partial class DetailViewModelBase : ViewModelBase
    {
        protected readonly IDialogService _dialogService;
        protected readonly ILogger _logger;
        protected readonly IPdfService _pdfService;

        [ObservableProperty] private int _animationPulse;
        [ObservableProperty] private bool _hasErrors;
        public ObservableCollection<string> ValidationErrors { get; } = new();

        protected DetailViewModelBase(IDialogService dialogService, ILogger logger, IPdfService pdfService)
        {
            _dialogService = dialogService;
            _logger = logger;
            _pdfService = pdfService;
        }

        [RelayCommand]
        public async Task PrintAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Generating report...";
                
                if (_pdfService == null)
                {
                    _logger?.LogError("IPdfService is not initialized. Ensure it is registered in the DI container.");
                    NotifyError("Print Error", "The PDF generation service is currently unavailable.");
                    return;
                }

                var path = await _pdfService.GenerateDetailReportPdfAsync(GetReportTitle(), GetReportItem());
                
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing detail report");
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected abstract string GetReportTitle();
        protected abstract object GetReportItem();

        [RelayCommand]
        public async Task Save()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                BusyText = "Saving changes...";

                if (await ValidateAsync())
                {
                    await ExecuteSaveAsync();
                    OnSaveSuccess();
                }
            }
            catch (ConcurrencyException cex)
            {
                _logger.LogWarning("Concurrency conflict detected: {Message}", cex.Message);
                var result = await _dialogService.ShowConflictResolutionAsync(
                    "Conflict Detected",
                    $"{cex.Message}\n\nAnother user has modified this record while you were editing. Choose how to proceed:");

                if (result == CustomDialogResult.Secondary)
                {
                    await ExecuteReloadAsync();
                }
                else if (result == CustomDialogResult.Primary)
                {
                    if (await ExecuteForceSaveAsync())
                    {
                        OnSaveSuccess();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during save operation");
                NotifyError("Save Error", $"Failed to save changes: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            OnCancel();
        }

        protected abstract Task ExecuteSaveAsync();
        protected abstract Task ExecuteReloadAsync();
        protected virtual Task<bool> ExecuteForceSaveAsync() => Task.FromResult(false);
        
        protected virtual Task<bool> ValidateAsync() => Task.FromResult(true);
        protected virtual void OnSaveSuccess() { }
        protected virtual void OnCancel() { }

        protected async Task PulseValidationAsync()
        {
            AnimationPulse = 0;
            await Task.Delay(100);
            AnimationPulse = 1;
        }
    }
}
