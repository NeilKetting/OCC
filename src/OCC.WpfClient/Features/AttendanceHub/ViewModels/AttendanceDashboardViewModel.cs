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
    public partial class AttendanceDashboardViewModel : OverlayHostViewModel
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IEmployeeService _employeeService;
        private readonly IProjectService _projectService;
        private readonly IDialogService _dialogService;
        private readonly IPdfService _pdfService;
        private readonly ILogger<AttendanceDashboardViewModel> _logger;

        private List<AttendanceStatusRow> _allRows = new();

        [ObservableProperty] private ObservableCollection<AttendanceStatusRow> _todayRows = new();
        [ObservableProperty] private int _presentCount;
        [ObservableProperty] private int _absentCount;
        [ObservableProperty] private int _lateCount;
        [ObservableProperty] private int _totalExpected;
        [ObservableProperty] private double _attendanceRate;
        [ObservableProperty] private string _todayDate = DateTime.Today.ToString("dddd, dd MMMM yyyy");
        [ObservableProperty] private bool _showNotPresentOnly;
        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private int _selectedBranchIndex = 0;
        [ObservableProperty] private string _selectedCardFilter = "All";

        partial void OnShowNotPresentOnlyChanged(bool value) => ApplyFilter();
        partial void OnSearchQueryChanged(string value) => ApplyFilter();
        partial void OnSelectedBranchIndexChanged(int value) => ApplyFilter();
        partial void OnSelectedCardFilterChanged(string value) => ApplyFilter();

        public AttendanceDashboardViewModel(
            IAttendanceService attendanceService,
            IEmployeeService employeeService,
            IProjectService projectService,
            IDialogService dialogService,
            IPdfService pdfService,
            ILogger<AttendanceDashboardViewModel> logger)
        {
            _attendanceService = attendanceService;
            _employeeService = employeeService;
            _projectService = projectService;
            _dialogService = dialogService;
            _pdfService = pdfService;
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
                var activeEmployees = employees.Where(e => e.Status == EmployeeStatus.Active).ToList();

                var today = DateTime.Today;
                var records = (await _attendanceService.GetTodaysAttendanceAsync())
                    .Where(r => r.Date.Date == today)
                    .ToList();

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
                        IsClocked = openRecord != null,
                        IsAutoClockIn = record?.IsAutoClockIn ?? false,
                        Record = record
                    });
                }

                _allRows = rows;
                ApplyFilter();

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

        private void ApplyFilter()
        {
            var filtered = _allRows.AsEnumerable();

            // Card filter
            if (SelectedCardFilter == "Present")
            {
                filtered = filtered.Where(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late);
            }
            else if (SelectedCardFilter == "Absent")
            {
                filtered = filtered.Where(r => r.Status == AttendanceStatus.Absent);
            }
            else if (SelectedCardFilter == "Late")
            {
                filtered = filtered.Where(r => r.Status == AttendanceStatus.Late);
            }

            // Search query
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLower();
                filtered = filtered.Where(r => 
                    r.EmployeeName.ToLower().Contains(q) || 
                    r.EmployeeNumber.ToLower().Contains(q) || 
                    r.Role.ToLower().Contains(q));
            }

            // Branch filter
            filtered = SelectedBranchIndex switch
            {
                1 => filtered.Where(r => r.Branch == "Johannesburg"),
                2 => filtered.Where(r => r.Branch == "Cape Town"),
                _ => filtered
            };

            // Not Present filter
            if (ShowNotPresentOnly)
            {
                filtered = filtered.Where(r => r.Status == AttendanceStatus.Absent || (r.IsClocked && r.IsAutoClockIn));
            }

            TodayRows = new ObservableCollection<AttendanceStatusRow>(
                filtered.OrderBy(r => r.Status == AttendanceStatus.Absent ? 1 : 0)
                        .ThenBy(r => r.EmployeeName));
        }

        [RelayCommand]
        private void ToggleNotPresentFilter() => ShowNotPresentOnly = !ShowNotPresentOnly;

        [RelayCommand]
        private void FilterByCard(string filterType)
        {
            if (filterType == "All")
            {
                SelectedCardFilter = "All";
            }
            else if (SelectedCardFilter == filterType)
            {
                SelectedCardFilter = "All";
            }
            else
            {
                SelectedCardFilter = filterType;
            }
        }

        [RelayCommand]
        private async Task MarkAbsentEmployee(AttendanceStatusRow? row)
        {
            if (row == null) return;
            try
            {
                var success = await _attendanceService.MarkAbsentAsync(row.EmployeeId, row.Branch);
                if (success)
                {
                    NotifySuccess("Marked Absent", $"{row.EmployeeName} has been marked absent.");
                    await LoadDashboardDataAsync();
                }
                else
                {
                    NotifyError("Failed", $"Could not mark {row.EmployeeName} as absent.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking employee {Id} absent", row.EmployeeId);
                NotifyError("Error", ex.Message);
            }
        }

        [RelayCommand]
        private async Task ClockInEmployee(AttendanceStatusRow? row)
        {
            if (row == null) return;

            if (row.IsClocked)
            {
                NotifyError("Already Clocked In", $"{row.EmployeeName} already has an active shift. Please clock them out first.");
                await LoadDashboardDataAsync();
                return;
            }

            var reason = await _dialogService.ShowInputDialogAsync("Late Arrival / Presence Note", "Enter reason for arriving late / note (optional):");
            if (reason == null) return; // User cancelled

            try
            {
                var now = DateTime.Now;
                var record = new AttendanceRecord
                {
                    EmployeeId = row.EmployeeId,
                    Date = now.Date,
                    CheckInTime = now,
                    ClockInTime = now.TimeOfDay,
                    Branch = row.Branch,
                    Status = AttendanceStatus.Present,
                    Notes = !string.IsNullOrWhiteSpace(reason) ? $"Arrival/Late Note: {reason}" : string.Empty
                };

                await _attendanceService.CreateAttendanceRecordAsync(record);
                NotifySuccess("Clocked In", $"{row.EmployeeName} has been clocked in.");
                await LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clocking in employee {Id}", row.EmployeeId);
                NotifyError("Clock-In Failed", ex.Message);
                await LoadDashboardDataAsync();
            }
        }

        [RelayCommand]
        private async Task ClockOutEmployee(AttendanceStatusRow? row)
        {
            if (row == null) return;

            // Find the record to update
            var record = row.Record;
            if (record == null && row.RecordId != null)
            {
                // Fallback: look up in active rows or fetch todays attendance
                var today = DateTime.Today;
                var records = await _attendanceService.GetTodaysAttendanceAsync();
                record = records.FirstOrDefault(r => r.Id == row.RecordId.Value);
            }

            if (record == null)
            {
                NotifyError("Clock-Out Failed", "No active attendance record found to clock out.");
                return;
            }

            var reason = await _dialogService.ShowInputDialogAsync("Clock Out Reason", "Enter the reason for clocking out (optional):");
            if (reason == null) return; // User cancelled

            try
            {
                record.CheckOutTime = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    record.Notes = string.IsNullOrWhiteSpace(record.Notes)
                        ? $"Clock Out Reason: {reason}"
                        : $"{record.Notes}; Clock Out Reason: {reason}";
                }

                await _attendanceService.UpdateAttendanceRecordAsync(record);
                NotifySuccess("Clocked Out", $"{row.EmployeeName} has been clocked out.");
                await LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clocking out employee {Id}", row.EmployeeId);
                NotifyError("Clock-Out Failed", ex.Message);
            }
        }

        [RelayCommand]
        private void EditEmployee(AttendanceStatusRow? row)
        {
            if (row == null) return;

            var record = row.Record ?? new AttendanceRecord
            {
                Id = Guid.Empty,
                EmployeeId = row.EmployeeId,
                Date = DateTime.Today,
                Status = AttendanceStatus.Present
            };

            var detailVm = new AttendanceDetailViewModel(
                record,
                _attendanceService,
                _employeeService,
                _projectService,
                _dialogService,
                _logger,
                _pdfService);

            OpenOverlay(detailVm, async (res) =>
            {
                if (res != null) // Saved successfully
                {
                    await LoadDashboardDataAsync();
                }
            });
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
        public bool IsAutoClockIn { get; set; }
        public AttendanceRecord? Record { get; set; }
        public bool IsAbsent => Status == AttendanceStatus.Absent;

        public string StatusLabel => (CheckInTime.HasValue && CheckOutTime.HasValue && Status != AttendanceStatus.Absent)
            ? "Clocked Out"
             : Status switch
            {
                AttendanceStatus.Present => "Present",
                AttendanceStatus.Late => "Late",
                AttendanceStatus.Absent => "Absent",
                AttendanceStatus.Sick => "Sick",
                AttendanceStatus.LeaveAuthorized => "Leave",
                AttendanceStatus.LeaveEarly => "Left Early",
                AttendanceStatus.UnpaidSick => "Unpaid Sick",
                AttendanceStatus.UnpaidLeave => "Unpaid Leave",
                _ => "Unknown"
            };

        public string StatusColor => (CheckInTime.HasValue && CheckOutTime.HasValue && Status != AttendanceStatus.Absent)
            ? "#78909C" // Blue-grey for clocked out
            : Status switch
            {
                AttendanceStatus.Present => "#2E7D32",
                AttendanceStatus.Late => "#F57F17",
                AttendanceStatus.Absent => "#C62828",
                AttendanceStatus.Sick => "#0288D1",
                AttendanceStatus.LeaveAuthorized => "#6A1B9A",
                AttendanceStatus.LeaveEarly => "#E65100",
                AttendanceStatus.UnpaidSick => "#8E24AA",
                AttendanceStatus.UnpaidLeave => "#5E35B1",
                _ => "#607D8B"
            };

        public string CheckInDisplay => (Status == AttendanceStatus.Absent || !CheckInTime.HasValue) ? "--:--" : CheckInTime.Value.ToString("HH:mm");
        public string CheckOutDisplay => (Status == AttendanceStatus.Absent || !CheckOutTime.HasValue) ? (IsClocked ? "Active" : "--:--") : CheckOutTime.Value.ToString("HH:mm");
        public string HoursDisplay => HoursWorked > 0 ? $"{HoursWorked:F1}h" : IsClocked ? "Active" : "-";
    }
}
