using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Client.Data;
using OCC.Client.Infrastructure;
using OCC.Client.Services;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Threading.Tasks;

namespace OCC.Client.Features.SettingsHub.ViewModels
{
    public partial class CompanySettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<CompanySettingsViewModel> _logger;
        private readonly IToastService _toastService;
        private readonly IPermissionService _permissionService;
        
        public bool IsAdmin => _permissionService.CanAccess(NavigationRoutes.UserManagement); // Simple check for admin

        [ObservableProperty]
        private CompanyDetails _companyDetails = new();

        public bool IsMondayEnabled
        {
            get => CompanyDetails.AutoClockInDays.Contains(DayOfWeek.Monday);
            set { UpdateDay(DayOfWeek.Monday, value); OnPropertyChanged(); }
        }
        public bool IsTuesdayEnabled
        {
            get => CompanyDetails.AutoClockInDays.Contains(DayOfWeek.Tuesday);
            set { UpdateDay(DayOfWeek.Tuesday, value); OnPropertyChanged(); }
        }
        public bool IsWednesdayEnabled
        {
            get => CompanyDetails.AutoClockInDays.Contains(DayOfWeek.Wednesday);
            set { UpdateDay(DayOfWeek.Wednesday, value); OnPropertyChanged(); }
        }
        public bool IsThursdayEnabled
        {
            get => CompanyDetails.AutoClockInDays.Contains(DayOfWeek.Thursday);
            set { UpdateDay(DayOfWeek.Thursday, value); OnPropertyChanged(); }
        }
        public bool IsFridayEnabled
        {
            get => CompanyDetails.AutoClockInDays.Contains(DayOfWeek.Friday);
            set { UpdateDay(DayOfWeek.Friday, value); OnPropertyChanged(); }
        }
        public bool IsSaturdayEnabled
        {
            get => CompanyDetails.AutoClockInDays.Contains(DayOfWeek.Saturday);
            set { UpdateDay(DayOfWeek.Saturday, value); OnPropertyChanged(); }
        }
        public bool IsSundayEnabled
        {
            get => CompanyDetails.AutoClockInDays.Contains(DayOfWeek.Sunday);
            set { UpdateDay(DayOfWeek.Sunday, value); OnPropertyChanged(); }
        }

        private void UpdateDay(DayOfWeek day, bool enabled)
        {
            if (enabled && !CompanyDetails.AutoClockInDays.Contains(day))
                CompanyDetails.AutoClockInDays.Add(day);
            else if (!enabled && CompanyDetails.AutoClockInDays.Contains(day))
                CompanyDetails.AutoClockInDays.Remove(day);
        }

        private void RefreshDays()
        {
            OnPropertyChanged(nameof(IsMondayEnabled));
            OnPropertyChanged(nameof(IsTuesdayEnabled));
            OnPropertyChanged(nameof(IsWednesdayEnabled));
            OnPropertyChanged(nameof(IsThursdayEnabled));
            OnPropertyChanged(nameof(IsFridayEnabled));
            OnPropertyChanged(nameof(IsSaturdayEnabled));
            OnPropertyChanged(nameof(IsSundayEnabled));
        }

        [ObservableProperty]
        private bool _isSaving;

        public CompanySettingsViewModel(
            ISettingsService settingsService, 
            IDialogService dialogService, 
            ILogger<CompanySettingsViewModel> logger,
            IToastService toastService,
            IPermissionService permissionService)
        {
            _settingsService = settingsService;
            _dialogService = dialogService;
            _logger = logger;
            _toastService = toastService;
            _permissionService = permissionService;

            LoadData();
        }
        
        public CompanySettingsViewModel() 
        { 
             // Design time
             _settingsService = null!;
             _dialogService = null!;
             _logger = null!;
             _toastService = null!;
             _permissionService = null!;
             CompanyDetails = new CompanyDetails();
        }

        private async void LoadData()
        {
            IsBusy = true;
            try
            {
                var details = await _settingsService.GetCompanyDetailsAsync();
                if (details != null)
                {
                    CompanyDetails = details;
                    RefreshDays();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load company settings.");
                _toastService.ShowError("Error", "Failed to load settings.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            if (IsSaving) return;
            IsSaving = true;
            try
            {
                await _settingsService.SaveCompanyDetailsAsync(CompanyDetails);
                _toastService.ShowSuccess("Success", "Settings saved successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save company settings.");
                _toastService.ShowError("Error", "Failed to save settings.");
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
