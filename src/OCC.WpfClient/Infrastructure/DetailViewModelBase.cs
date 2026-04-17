using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Infrastructure.Exceptions;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace OCC.WpfClient.Infrastructure
{
    public abstract partial class DetailViewModelBase : ViewModelBase
    {
        protected readonly IDialogService _dialogService;
        protected readonly ILogger _logger;

        protected DetailViewModelBase(IDialogService dialogService, ILogger logger)
        {
            _dialogService = dialogService;
            _logger = logger;
        }

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
                await _dialogService.ShowAlertAsync("Error", $"Failed to save changes: {ex.Message}");
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
    }
}
