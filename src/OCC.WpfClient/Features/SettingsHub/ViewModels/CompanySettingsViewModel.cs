using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.SettingsHub.ViewModels
{
    public partial class CompanySettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IAuditLogService _auditLogService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<CompanySettingsViewModel> _logger;
        private readonly IToastService _toastService;
        private readonly IPermissionService _permissionService;
        
        public bool IsAdmin => _permissionService.CanAccess(NavigationRoutes.UserManagement); 

        [ObservableProperty]
        private CompanyDetails _companyDetails = new();

        [ObservableProperty]
        private int _totalAuditLogsCount;

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

        public int AuditLogRetentionMonthsIndex
        {
            get
            {
                return CompanyDetails.AuditLogRetentionMonths switch
                {
                    1 => 1,
                    3 => 2,
                    6 => 3,
                    12 => 4,
                    _ => 0
                };
            }
            set
            {
                CompanyDetails.AuditLogRetentionMonths = value switch
                {
                    1 => 1,
                    2 => 3,
                    3 => 6,
                    4 => 12,
                    _ => 0
                };
                OnPropertyChanged();
            }
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
            IAuditLogService auditLogService,
            IDialogService dialogService, 
            ILogger<CompanySettingsViewModel> logger,
            IToastService toastService,
            IPermissionService permissionService)
        {
            _settingsService = settingsService;
            _auditLogService = auditLogService;
            _dialogService = dialogService;
            _logger = logger;
            _toastService = toastService;
            _permissionService = permissionService;

            Title = "System Settings";
            LoadData();
        }

        private async void LoadData()
        {
            IsBusy = true;
            try
            {
                var detailsTask = _settingsService.GetCompanyDetailsAsync();
                var logCountTask = _auditLogService.GetTotalCountAsync();

                await Task.WhenAll(detailsTask, logCountTask);

                var details = detailsTask.Result;
                if (details != null)
                {
                    details.AutoClockInDays ??= new System.Collections.Generic.List<DayOfWeek>();
                    CompanyDetails = details;
                    RefreshDays();
                    OnPropertyChanged(nameof(AuditLogRetentionMonthsIndex));
                }

                TotalAuditLogsCount = logCountTask.Result;
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

        [RelayCommand]
        public void Close()
        {
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }
    }
}
