using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System.Collections.ObjectModel;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    /// <summary>
    /// Dashboard showing live today's rollcall — who is clocked in, who hasn't arrived, totals.
    /// </summary>
    public partial class AttendanceDashboardViewModel : ViewModelBase
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IEmployeeService _employeeService;
        private readonly ILogger<AttendanceDashboardViewModel> _logger;

        [ObservableProperty] private ObservableCollection<AttendanceStatusRow> _todayRows = new();
        [ObservableProperty] private int _presentCount;
        [ObservableProperty] private int _absentCount;
        [ObservableProperty] private int _lateCount;
        [ObservableProperty] private int _totalExpected;
        [ObservableProperty] private double _attendanceRate;
        [ObservableProperty] private string _todayDate = DateTime.Today.ToString("dddd, dd MMMM yyyy");

        public AttendanceDashboardViewModel(
            IAttendanceService attendanceService,
            IEmployeeService employeeService,
            ILogger<AttendanceDashboardViewModel> logger)
        {
            _attendanceService = attendanceService;
            _employeeService = employeeService;
            _logger = logger;
            Title = "Attendance Dashboard";
        }

        [RelayCommand]
        public async Task LoadDashboardDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading today's attendance...";

                var employees = await _employeeService.GetEmployeesAsync();
                var activeEmployees = employees.ToList();

                var records = (await _attendanceService.GetTodaysAttendanceAsync()).ToList();

                var rows = new List<AttendanceStatusRow>();
                foreach (var emp in activeEmployees)
                {
                    var empRecords = records.Where(r => r.EmployeeId == emp.Id).ToList();

                    // Prefer the open (active) record so IsClocked is always accurate.
                    // An employee may have a closed morning record + an open afternoon shift,
                    // or vice-versa – we always surface the open one if it exists.
                    var openRecord = empRecords.FirstOrDefault(r => r.CheckInTime.HasValue && r.CheckOutTime == null);
                    var record = openRecord ?? empRecords.OrderByDescending(r => r.CheckInTime).FirstOrDefault();

                    rows.Add(new AttendanceStatusRow
                    {
                        EmployeeId = emp.Id,
                        EmployeeName = $"{emp.FirstName} {emp.LastName}",
                        EmployeeNumber = emp.EmployeeNumber ?? string.Empty,
                        Role = emp.Role.ToString(),
                        Branch = emp.Branch ?? string.Empty,
                        Status = record?.Status ?? AttendanceStatus.Absent,
                        CheckInTime = record?.CheckInTime,
                        CheckOutTime = record?.CheckOutTime,
                        HoursWorked = record?.HoursWorked ?? 0,
                        RecordId = record?.Id,
                        IsClocked = openRecord != null
                    });
                }

                TodayRows = new ObservableCollection<AttendanceStatusRow>(
                    rows.OrderBy(r => r.Status == AttendanceStatus.Absent ? 1 : 0)
                        .ThenBy(r => r.EmployeeName));

                TotalExpected = rows.Count;
                PresentCount = rows.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late);
                LateCount = rows.Count(r => r.Status == AttendanceStatus.Late);
                AbsentCount = rows.Count(r => r.Status == AttendanceStatus.Absent);
                AttendanceRate = TotalExpected > 0 ? Math.Round((double)PresentCount / TotalExpected * 100, 1) : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attendance dashboard");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ClockInEmployee(AttendanceStatusRow? row)
        {
            if (row == null) return;

            // Guard: if the row is already showing as clocked in the UI should have hidden
            // the button, but protect against race conditions / stale data.
            if (row.IsClocked)
            {
                NotifyError("Already Clocked In", $"{row.EmployeeName} already has an active shift. Please clock them out first.");
                await LoadDashboardDataAsync(); // Refresh so the UI reflects reality.
                return;
            }

            try
            {
                await _attendanceService.ClockInAsync(row.EmployeeId, row.Branch);
                NotifySuccess("Clocked In", $"{row.EmployeeName} has been clocked in.");
                await LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clocking in employee {Id}", row.EmployeeId);
                NotifyError("Clock-In Failed", ex.Message);
                await LoadDashboardDataAsync(); // Refresh so stale state doesn't persist.
            }
        }

        [RelayCommand]
        private async Task ClockOutEmployee(AttendanceStatusRow? row)
        {
            if (row?.RecordId == null) return;
            try
            {
                await _attendanceService.ClockOutAsync(row.RecordId.Value);
                NotifySuccess("Clocked Out", $"{row.EmployeeName} has been clocked out.");
                await LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clocking out employee {Id}", row.EmployeeId);
                NotifyError("Clock-Out Failed", ex.Message);
            }
        }
    }

    public class AttendanceStatusRow
    {
        public Guid EmployeeId { get; set; }
        public Guid? RecordId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public AttendanceStatus Status { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public double HoursWorked { get; set; }
        public bool IsClocked { get; set; }

        public string StatusLabel => Status switch
        {
            AttendanceStatus.Present => "Present",
            AttendanceStatus.Late => "Late",
            AttendanceStatus.Absent => "Absent",
            AttendanceStatus.Sick => "Sick",
            AttendanceStatus.LeaveAuthorized => "Leave",
            AttendanceStatus.LeaveEarly => "Left Early",
            AttendanceStatus.UnpaidSick => "Unpaid Sick",
            _ => "Unknown"
        };

        public string StatusColor => Status switch
        {
            AttendanceStatus.Present => "#2E7D32",
            AttendanceStatus.Late => "#F57F17",
            AttendanceStatus.Absent => "#C62828",
            AttendanceStatus.Sick => "#0288D1",
            AttendanceStatus.LeaveAuthorized => "#6A1B9A",
            AttendanceStatus.LeaveEarly => "#E65100",
            _ => "#607D8B"
        };

        public string CheckInDisplay => CheckInTime.HasValue ? CheckInTime.Value.ToString("HH:mm") : "--:--";
        public string CheckOutDisplay => CheckOutTime.HasValue ? CheckOutTime.Value.ToString("HH:mm") : IsClocked ? "Active" : "--:--";
        public string HoursDisplay => HoursWorked > 0 ? $"{HoursWorked:F1}h" : IsClocked ? "Active" : "-";
    }
}
