using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class DailyTimesheetV2ViewModel : ViewModelBase, IRecipient<EntityUpdatedMessage>
    {
        private readonly ITimeServiceV2 _timeServiceV2;
        private readonly IEmployeeService _employeeService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<DailyTimesheetV2ItemViewModel> _timesheets = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private DateTime _date = DateTime.Today;

        public DailyTimesheetV2ViewModel(ITimeServiceV2 timeServiceV2, IEmployeeService employeeService, IDialogService dialogService)
        {
            _timeServiceV2 = timeServiceV2;
            _employeeService = employeeService;
            _dialogService = dialogService;

            Title = "Daily Timesheet V2 (Payroll)";
            WeakReferenceMessenger.Default.Register<EntityUpdatedMessage>(this);
        }

        async partial void OnDateChanged(DateTime value)
        {
            await LoadDataAsync();
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                var timesheets = await _timeServiceV2.GetDailyTimesheetsAsync(Date);
                var employees = await _employeeService.GetEmployeesAsync();

                var eventVms = timesheets.Select(t => 
                {
                    var emp = employees.FirstOrDefault(emp => emp.Id == t.EmployeeId);
                    return new DailyTimesheetV2ItemViewModel
                    {
                        TimesheetId = t.Id,
                        EmployeeId = t.EmployeeId,
                        EmployeeName = emp != null ? $"{emp.FirstName} {emp.LastName}" : "Unknown",
                        Status = t.Status,
                        FirstInTime = t.FirstInTime,
                        LastOutTime = t.LastOutTime,
                        CalculatedHours = t.CalculatedHours,
                        WageEstimated = t.WageEstimated
                    };
                }).OrderBy(x => x.EmployeeName).ToList();

                Timesheets = new ObservableCollection<DailyTimesheetV2ItemViewModel>(eventVms);
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"[DailyTimesheetV2] Load Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async void Receive(EntityUpdatedMessage message)
        {
             // Realistically we only care if it's today's sheet, but simple reload is fine for V2 test
            if (message.Value.EntityType == "ClockingEvent" || message.Value.EntityType == "DailyTimesheet")
            {
                await LoadDataAsync();
            }
        }
    }

    public partial class DailyTimesheetV2ItemViewModel : ObservableObject
    {
        public Guid TimesheetId { get; set; }
        public Guid EmployeeId { get; set; }
        
        [ObservableProperty]
        private string _employeeName = string.Empty;
        
        [ObservableProperty]
        private TimesheetStatus _status;
        
        [ObservableProperty]
        private DateTime? _firstInTime;
        
        [ObservableProperty]
        private DateTime? _lastOutTime;
        
        [ObservableProperty]
        private decimal _calculatedHours;
        
        [ObservableProperty]
        private decimal _wageEstimated;
    }
}
