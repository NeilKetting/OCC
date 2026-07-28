using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Infrastructure;
using OCC.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace OCC.WpfClient.Features.MainHub.ViewModels;

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
        public string Type { get; set; } = string.Empty; // e.g. Passport, Medical, Certificate
        public string ActionParameter { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; } = AlertSeverity.Critical;

        public string BorderBrushColor => Severity == AlertSeverity.Critical ? "#30E81123" : "#30F97316";
        public string BackgroundColor => Severity == AlertSeverity.Critical ? "#1AE81123" : "#1AF97316";
        public string HoverBorderColor => Severity == AlertSeverity.Critical ? "#E81123" : "#F97316";
        public string HoverBackgroundColor => Severity == AlertSeverity.Critical ? "#33E81123" : "#33F97316";
        public string IconColor => Severity == AlertSeverity.Critical ? "#EF4444" : "#F97316";
    }

    public partial class AlertsWidgetViewModel : WidgetViewModelBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly IPermissionService _permissionService;

        public ObservableCollection<SystemAlertItem> Alerts { get; } = new();

        [ObservableProperty]
        private int _alertCount;

        public AlertsWidgetViewModel(IEmployeeService employeeService, IPermissionService permissionService)
        {
            _employeeService = employeeService;
            _permissionService = permissionService;
            WidgetId = "Alerts";
            Title = "Action Center";
        }

        [RelayCommand]
        private void ResolveAlert(SystemAlertItem alert)
        {
            if (!_permissionService.CanAccess(NavigationRoutes.StaffManagement))
            {
                NotifyError("Access Denied", "You do not have permission to view Staff Management.");
                return;
            }
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.StaffManagement));
        }

        public override async Task RefreshDataAsync()
        {
            try
            {
                var today = DateTime.Today;
                var employees = await _employeeService.GetEmployeesAsync();

                var expiringPassports = employees
                    .Where(e => e.Status == EmployeeStatus.Active && e.IdType == IdType.Passport &&
                                (!e.PassportStampDate.HasValue || (e.PassportStampDate.Value.Date.AddDays(90) - today).TotalDays <= 60))
                    .ToList();

                App.Current.Dispatcher.Invoke(() =>
                {
                    Alerts.Clear();

                    foreach (var emp in expiringPassports)
                    {
                        var remainingDays = emp.PassportStampDate.HasValue 
                            ? (int)(90 - (today - emp.PassportStampDate.Value.Date).TotalDays)
                            : 0;

                        string msg;
                        AlertSeverity severity = AlertSeverity.Critical;

                        if (!emp.PassportStampDate.HasValue)
                        {
                            msg = "Passport stamp date is not set!";
                            severity = AlertSeverity.Critical;
                        }
                        else if (remainingDays < 0)
                        {
                            msg = $"Passport stamp expired {-remainingDays} days ago (stamped on {emp.PassportStampDate.Value:yyyy-MM-dd}).";
                            severity = AlertSeverity.Critical;
                        }
                        else if (remainingDays == 0)
                        {
                            msg = $"Passport stamp expires today (stamped on {emp.PassportStampDate.Value:yyyy-MM-dd}).";
                            severity = AlertSeverity.Critical;
                        }
                        else
                        {
                            msg = $"Passport stamp expires in {remainingDays} days (stamped on {emp.PassportStampDate.Value:yyyy-MM-dd}).";
                            severity = AlertSeverity.Warning;
                        }

                        Alerts.Add(new SystemAlertItem
                        {
                            Title = $"{emp.DisplayName} - Passport Stamp Expiry",
                            Message = msg,
                            Icon = "\uE7BA", // warning icon
                            Type = "Passport",
                            ActionParameter = emp.Id.ToString(),
                            Severity = severity
                        });
                    }

                    AlertCount = Alerts.Count;
                    // Visible if there are alerts to show
                    IsVisible = AlertCount > 0;
                });
            }
            catch { }
        }
    }
