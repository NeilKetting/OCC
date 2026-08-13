using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.Main.ViewModels;

public enum AlertSeverity
{
    Critical,
    Warning
}

public class SystemAlertItem
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // e.g. Passport, Banking, Medical
    public string ActionParameter { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Critical;

    public string BorderBrushColor => Severity == AlertSeverity.Critical ? "#30E81123" : "#30F97316";
    public string BackgroundColor => Severity == AlertSeverity.Critical ? "#1AE81123" : "#1AF97316";
    public string HoverBorderColor => Severity == AlertSeverity.Critical ? "#E81123" : "#F97316";
    public string HoverBackgroundColor => Severity == AlertSeverity.Critical ? "#33E81123" : "#33F97316";
    public string IconColor => Severity == AlertSeverity.Critical ? "#EF4444" : "#F97316";
}

public partial class SystemAlertGroupItem : ObservableObject
{
    public string GroupTitle { get; set; } = string.Empty;
    public string CategoryType { get; set; } = string.Empty; // PassportExpired, PassportExpiringSoon, Banking
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public string Icon { get; set; } = "\uE7BA";
    public ObservableCollection<SystemAlertItem> Items { get; } = new();

    [ObservableProperty]
    private bool _isExpanded;

    public int BadgeCount => Items.Count;

    public string BorderBrushColor => Severity == AlertSeverity.Critical ? "#30E81123" : "#30F97316";
    public string BackgroundColor => Severity == AlertSeverity.Critical ? "#1AE81123" : "#1AF97316";
    public string HoverBorderColor => Severity == AlertSeverity.Critical ? "#E81123" : "#F97316";
    public string HoverBackgroundColor => Severity == AlertSeverity.Critical ? "#33E81123" : "#33F97316";
    public string IconColor => Severity == AlertSeverity.Critical ? "#EF4444" : "#F97316";

    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }
}

public partial class AlertsWidgetViewModel : WidgetViewModelBase
{
    private readonly IEmployeeService _employeeService;
    private readonly IPermissionService _permissionService;
    private readonly LocalSettingsService _localSettingsService;

    public ObservableCollection<SystemAlertGroupItem> AlertGroups { get; } = new();

    [ObservableProperty] private int _alertCount;
    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private bool _canAccessStaffManagement;
    [ObservableProperty] private bool _trackPassportAlerts;
    [ObservableProperty] private bool _trackBankingAlerts;

    public AlertsWidgetViewModel(
        IEmployeeService employeeService,
        IPermissionService permissionService,
        LocalSettingsService localSettingsService)
    {
        _employeeService = employeeService;
        _permissionService = permissionService;
        _localSettingsService = localSettingsService;
        WidgetId = "Alerts";
        Title = "Action Center";

        CanAccessStaffManagement = _permissionService.CanAccess(NavigationRoutes.StaffManagement);
        TrackPassportAlerts = _localSettingsService.Settings.ActionCenterTrackPassportAlerts;
        TrackBankingAlerts = _localSettingsService.Settings.ActionCenterTrackBankingAlerts;
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    partial void OnTrackPassportAlertsChanged(bool value)
    {
        _localSettingsService.Settings.ActionCenterTrackPassportAlerts = value;
        _localSettingsService.Save();
        _ = RefreshDataAsync();
    }

    partial void OnTrackBankingAlertsChanged(bool value)
    {
        _localSettingsService.Settings.ActionCenterTrackBankingAlerts = value;
        _localSettingsService.Save();
        _ = RefreshDataAsync();
    }

    [RelayCommand]
    private void ResolveAlert(SystemAlertItem alert)
    {
        if (!_permissionService.CanAccess(NavigationRoutes.StaffManagement))
        {
            NotifyError("Access Denied", "You do not have permission to view Staff Management.");
            return;
        }

        if (Guid.TryParse(alert?.ActionParameter, out var empId))
        {
            WeakReferenceMessenger.Default.Send(new OpenEmployeeMessage(empId, alert?.Type));
        }
        else
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.StaffManagement));
        }
    }

    public override async Task RefreshDataAsync()
    {
        try
        {
            var today = DateTime.Today;
            var employees = await _employeeService.GetEmployeesAsync();

            void PopulateAlerts()
            {
                // Save expanded group keys to preserve expand state
                var expandedKeys = AlertGroups.Where(g => g.IsExpanded).Select(g => g.CategoryType).ToHashSet();

                AlertGroups.Clear();

                if (CanAccessStaffManagement)
                {
                    // 1. Passport Stamp Expirations
                    if (TrackPassportAlerts)
                    {
                        var passportEmps = employees
                            .Where(e => e.Status == EmployeeStatus.Active && e.IdType == IdType.Passport)
                            .ToList();

                        var expiredGroup = new SystemAlertGroupItem
                        {
                            GroupTitle = "Expired Passport Stamps",
                            CategoryType = "PassportExpired",
                            Severity = AlertSeverity.Critical,
                            Icon = "\uE7BA",
                            IsExpanded = expandedKeys.Contains("PassportExpired")
                        };

                        var expiringSoonGroup = new SystemAlertGroupItem
                        {
                            GroupTitle = "Passports Expiring Soon",
                            CategoryType = "PassportExpiringSoon",
                            Severity = AlertSeverity.Warning,
                            Icon = "\uE7BA",
                            IsExpanded = expandedKeys.Contains("PassportExpiringSoon")
                        };

                        foreach (var emp in passportEmps)
                        {
                            var remainingDays = emp.PassportStampDate.HasValue
                                ? (int)(90 - (today - emp.PassportStampDate.Value.Date).TotalDays)
                                : 0;

                            if (!emp.PassportStampDate.HasValue || remainingDays <= 0)
                            {
                                string msg = !emp.PassportStampDate.HasValue
                                    ? "Passport stamp date is not set!"
                                    : $"Passport stamp expired {-remainingDays} days ago (stamped on {emp.PassportStampDate.Value:yyyy-MM-dd}).";

                                expiredGroup.Items.Add(new SystemAlertItem
                                {
                                    Title = emp.DisplayName,
                                    Message = msg,
                                    Icon = "\uE7BA",
                                    Type = "Passport",
                                    ActionParameter = emp.Id.ToString(),
                                    Severity = AlertSeverity.Critical
                                });
                            }
                            else if (remainingDays <= 60)
                            {
                                string msg = $"Passport stamp expires in {remainingDays} days (stamped on {emp.PassportStampDate.Value:yyyy-MM-dd}).";

                                expiringSoonGroup.Items.Add(new SystemAlertItem
                                {
                                    Title = emp.DisplayName,
                                    Message = msg,
                                    Icon = "\uE7BA",
                                    Type = "Passport",
                                    ActionParameter = emp.Id.ToString(),
                                    Severity = AlertSeverity.Warning
                                });
                            }
                        }

                        if (expiredGroup.Items.Any()) AlertGroups.Add(expiredGroup);
                        if (expiringSoonGroup.Items.Any()) AlertGroups.Add(expiringSoonGroup);
                    }

                    // 2. Missing Banking Details
                    if (TrackBankingAlerts)
                    {
                        var missingBankEmps = employees
                            .Where(e => e.Status == EmployeeStatus.Active &&
                                        (string.IsNullOrWhiteSpace(e.BankName) ||
                                         (e.BankAccountNumber != null && string.IsNullOrWhiteSpace(e.BankAccountNumber))))
                            .ToList();

                        if (missingBankEmps.Any())
                        {
                            var bankingGroup = new SystemAlertGroupItem
                            {
                                GroupTitle = "Missing Banking Details",
                                CategoryType = "Banking",
                                Severity = AlertSeverity.Warning,
                                Icon = "\uE8EF", // bank/payment icon
                                IsExpanded = expandedKeys.Contains("Banking")
                            };

                            foreach (var emp in missingBankEmps)
                            {
                                string missingPart = string.IsNullOrWhiteSpace(emp.BankName) && string.IsNullOrWhiteSpace(emp.BankAccountNumber)
                                    ? "No bank name or account number recorded"
                                    : string.IsNullOrWhiteSpace(emp.BankName) ? "Bank name missing" : "Account number missing";

                                bankingGroup.Items.Add(new SystemAlertItem
                                {
                                    Title = emp.DisplayName,
                                    Message = missingPart,
                                    Icon = "\uE8EF",
                                    Type = "Banking",
                                    ActionParameter = emp.Id.ToString(),
                                    Severity = AlertSeverity.Warning
                                });
                            }

                            AlertGroups.Add(bankingGroup);
                        }
                    }
                }

                AlertCount = AlertGroups.Sum(g => g.BadgeCount);
                IsVisible = AlertCount > 0;
            }

            if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
            {
                App.Current.Dispatcher.Invoke(PopulateAlerts);
            }
            else
            {
                PopulateAlerts();
            }
        }
        catch { }
    }
}
