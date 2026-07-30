using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    /// <summary>
    /// ViewModel for managing system-wide customizable wage configuration settings.
    /// </summary>
    public partial class WageSettingsViewModel : ViewModelBase
    {
        private readonly IWageService _wageService;
        private readonly IToastService _toastService;
        private readonly ISignalRService _signalRService;
        private readonly ILogger<WageSettingsViewModel> _logger;

        [ObservableProperty] private WageSettings _settings = new();
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private bool _isSaving;

        public WageSettingsViewModel(
            IWageService wageService,
            IToastService toastService,
            ISignalRService signalRService,
            ILogger<WageSettingsViewModel> logger)
        {
            _wageService = wageService;
            _toastService = toastService;
            _signalRService = signalRService;
            _logger = logger;
            Title = "Wage System Settings";

            _signalRService.OnWageSettingsChanged += (change) =>
            {
                if (change?.Entity != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Settings = change.Entity;
                    });
                }
            };
        }

        [RelayCommand]
        public async Task LoadSettingsAsync()
        {
            IsLoading = true;
            try
            {
                Settings = await _wageService.GetWageSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading wage settings");
                _toastService.ShowError("Error", "Failed to load wage settings: " + ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task SaveSettingsAsync()
        {
            IsSaving = true;
            try
            {
                Settings = await _wageService.UpdateWageSettingsAsync(Settings);
                _toastService.ShowSuccess("Success", "Wage settings updated successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving wage settings");
                _toastService.ShowError("Error", "Failed to save wage settings: " + ex.Message);
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
