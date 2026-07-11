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

namespace OCC.WpfClient.Features.Main.ViewModels
{
    public class SystemAlertItem
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // e.g. Passport, Medical, Certificate
        public string ActionParameter { get; set; } = string.Empty;
    }

    public partial class AlertsWidgetViewModel : WidgetViewModelBase
    {
        private readonly IEmployeeService _employeeService;

        public ObservableCollection<SystemAlertItem> Alerts { get; } = new();

        [ObservableProperty]
        private int _alertCount;

        public AlertsWidgetViewModel(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
            WidgetId = "Alerts";
            Title = "Action Center";
        }

        [RelayCommand]
        private void ResolveAlert(SystemAlertItem alert)
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.StaffManagement));
        }

        public override async Task RefreshDataAsync()
        {
            try
            {
                var today = DateTime.Today;
                var employees = await _employeeService.GetEmployeesAsync();

                var expiringPassports = employees
                    .Where(e => e.Status == EmployeeStatus.Active && e.IsPassportStampExpired)
                    .ToList();

                App.Current.Dispatcher.Invoke(() =>
                {
                    Alerts.Clear();

                    foreach (var emp in expiringPassports)
                    {
                        var remainingDays = emp.PassportStampDate.HasValue 
                            ? (int)(90 - (today - emp.PassportStampDate.Value.Date).TotalDays)
                            : 0;

                        string msg = emp.PassportStampDate.HasValue
                            ? $"Passport stamp expires in {remainingDays} days (stamped on {emp.PassportStampDate.Value:yyyy-MM-dd})."
                            : "Passport stamp date is not set!";

                        Alerts.Add(new SystemAlertItem
                        {
                            Title = $"{emp.DisplayName} - Passport Stamp Expiry",
                            Message = msg,
                            Icon = "\uE7BA", // warning icon
                            Type = "Passport",
                            ActionParameter = emp.Id.ToString()
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
}
