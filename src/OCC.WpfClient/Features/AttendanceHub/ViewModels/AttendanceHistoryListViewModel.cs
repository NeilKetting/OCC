using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System.Collections.ObjectModel;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    /// <summary>
    /// List-based view of historical attendance records with date range filter,
    /// branch filter, search, and inline edit/delete support.
    /// </summary>
    public partial class AttendanceHistoryListViewModel : ListViewModelBase<AttendanceHistoryRow>
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IEmployeeService _employeeService;
        private readonly ILogger<AttendanceHistoryListViewModel> _logger;
        private List<AttendanceRecord> _allRecords = new();

        public override string ReportTitle => "Attendance History Report";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Employee",   PropertyName = "EmployeeName",  Width = 1.8 },
            new() { Header = "Date",       PropertyName = "Date",          Width = 1 },
            new() { Header = "Status",     PropertyName = "Status",        Width = 1 },
            new() { Header = "Clock In",   PropertyName = "CheckInTime",   Width = 1 },
            new() { Header = "Clock Out",  PropertyName = "CheckOutTime",  Width = 1 },
            new() { Header = "Hours",      PropertyName = "HoursWorked",   Width = 0.8 },
            new() { Header = "Branch",     PropertyName = "Branch",        Width = 1 },
        };

        public override IRelayCommand<object>? OpenCommand => EditRecordCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteRecordCommand;

        [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _toDate = DateTime.Today;
        [ObservableProperty] private int _selectedBranchIndex = 0;
        [ObservableProperty] private int _selectedStatusIndex = 0;
        [ObservableProperty] private int _totalHours;
        [ObservableProperty] private AttendanceRecord? _editingRecord;
        [ObservableProperty] private bool _isEditPanelOpen;
        [ObservableProperty] private string? _sickNoteFilePath;
        [ObservableProperty] private bool _hasSickNote;

        /// <summary>Status of the record before the edit panel was opened — used to guard balance deduction.</summary>
        private AttendanceStatus _previousStatus;

        // Rich employee name lookup for display
        private Dictionary<Guid, string> _employeeNameMap = new();

        public AttendanceHistoryListViewModel(
            IAttendanceService attendanceService,
            IEmployeeService employeeService,
            IPdfService pdfService,
            ILogger<AttendanceHistoryListViewModel> logger) : base(pdfService)
        {
            _attendanceService = attendanceService;
            _employeeService = employeeService;
            _logger = logger;
            Title = "Attendance History";
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading attendance records...";

                // Build employee name map for display
                var employees = await _employeeService.GetEmployeesAsync();
                _employeeNameMap = employees.ToDictionary(e => e.Id, e => $"{e.FirstName} {e.LastName}");

                _allRecords = (await _attendanceService.GetAttendanceRecordsAsync(FromDate, ToDate)).ToList();
                FilterItems();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attendance history");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnFromDateChanged(DateTime value) => _ = LoadDataAsync();
        partial void OnToDateChanged(DateTime value) => _ = LoadDataAsync();
        partial void OnSelectedBranchIndexChanged(int value) => FilterItems();
        partial void OnSelectedStatusIndexChanged(int value) => FilterItems();

        protected override void FilterItems()
        {
            var filtered = _allRecords.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLower();
                filtered = filtered.Where(r =>
                    (r.Branch?.ToLower().Contains(q) ?? false) ||
                    (r.EmployeeId.HasValue && _employeeNameMap.TryGetValue(r.EmployeeId.Value, out var name) && name.ToLower().Contains(q)));
            }

            filtered = SelectedBranchIndex switch
            {
                1 => filtered.Where(r => r.Branch == "Johannesburg"),
                2 => filtered.Where(r => r.Branch == "Cape Town"),
                _ => filtered
            };

            filtered = SelectedStatusIndex switch
            {
                1 => filtered.Where(r => r.Status == AttendanceStatus.Present),
                2 => filtered.Where(r => r.Status == AttendanceStatus.Late),
                3 => filtered.Where(r => r.Status == AttendanceStatus.Absent),
                4 => filtered.Where(r => r.Status == AttendanceStatus.Sick),
                5 => filtered.Where(r => r.Status == AttendanceStatus.LeaveAuthorized),
                _ => filtered
            };

            var result = filtered
                .OrderByDescending(r => r.Date)
                .Select(r => new AttendanceHistoryRow
                {
                    Record       = r,
                    EmployeeName = GetEmployeeName(r.EmployeeId)
                })
                .ToList();
            Items = new ObservableCollection<AttendanceHistoryRow>(result);
            TotalCount = result.Count;
            TotalHours = (int)result.Sum(r => r.Record.HoursWorked);
        }

        public string GetEmployeeName(Guid? id) =>
            id.HasValue && _employeeNameMap.TryGetValue(id.Value, out var n) ? n : "Unknown";

        [RelayCommand]
        private void EditRecord(object? parameter)
        {
            var row = parameter as AttendanceHistoryRow ?? SelectedItem;
            var record = row?.Record;
            if (record == null) return;

            // Capture status BEFORE opening panel for balance-deduction guard
            _previousStatus = record.Status;
            SickNoteFilePath = null;
            HasSickNote = false;

            EditingRecord = new AttendanceRecord
            {
                Id = record.Id,
                EmployeeId = record.EmployeeId,
                Date = record.Date,
                CheckInTime = record.CheckInTime,
                CheckOutTime = record.CheckOutTime,
                Status = record.Status,
                Branch = record.Branch,
                Notes = record.Notes,
                HoursWorked = record.HoursWorked,
                DoctorsNoteImagePath = record.DoctorsNoteImagePath,
                RowVersion = record.RowVersion
            };
            IsEditPanelOpen = true;
        }

        [RelayCommand]
        private async Task SaveRecord()
        {
            if (EditingRecord == null) return;
            try
            {
                IsBusy = true;

                // 1. Upload sick note if provided
                if (!string.IsNullOrEmpty(SickNoteFilePath))
                {
                    var serverPath = await _attendanceService.UploadSickNoteAsync(SickNoteFilePath);
                    if (!string.IsNullOrEmpty(serverPath))
                        EditingRecord.DoctorsNoteImagePath = serverPath;
                }

                // 2. Save the attendance record
                await _attendanceService.UpdateAttendanceRecordAsync(EditingRecord);

                // 3. Deduct sick leave balance if status changed TO Sick (and wasn't already Sick)
                if (EditingRecord.Status == AttendanceStatus.Sick &&
                    _previousStatus != AttendanceStatus.Sick &&
                    EditingRecord.EmployeeId.HasValue)
                {
                    try
                    {
                        var emp = await _employeeService.GetEmployeeAsync(EditingRecord.EmployeeId.Value);
                        if (emp != null)
                        {
                            // Deduct 1 sick day — clamp at 0
                            emp.SickLeaveBalance = Math.Max(0, emp.SickLeaveBalance - 1);
                            var updateEmp = new OCC.Shared.Models.Employee
                            {
                                Id = emp.Id,
                                FirstName = emp.FirstName,
                                LastName = emp.LastName,
                                EmployeeNumber = emp.EmployeeNumber ?? string.Empty,
                                IdNumber = emp.IdNumber,
                                Email = emp.Email,
                                Phone = emp.Phone,
                                Branch = emp.Branch,
                                Role = emp.Role,
                                Status = emp.Status,
                                HourlyRate = emp.HourlyRate,
                                SickLeaveBalance = emp.SickLeaveBalance,
                                AnnualLeaveBalance = emp.AnnualLeaveBalance,
                                ShiftStartTime = emp.ShiftStartTime,
                                ShiftEndTime = emp.ShiftEndTime,
                                RowVersion = emp.RowVersion
                            };
                            await _employeeService.UpdateEmployeeAsync(updateEmp);
                            NotifySuccess("Record Updated",
                                $"Status changed to Sick. 1 sick day deducted from {emp.FirstName} {emp.LastName}'s balance ({emp.SickLeaveBalance:F1} days remaining).");
                        }
                    }
                    catch (Exception balEx)
                    {
                        _logger.LogWarning(balEx, "Could not deduct sick leave balance for employee {Id}", EditingRecord.EmployeeId);
                        NotifySuccess("Record Updated", "Attendance record saved. Note: sick leave balance could not be updated automatically.");
                    }
                }
                else
                {
                    NotifySuccess("Saved", "Attendance record updated.");
                }

                IsEditPanelOpen = false;
                SickNoteFilePath = null;
                HasSickNote = false;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving attendance record");
                NotifyError("Save Failed", ex.Message);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void UploadSickNote()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Sick Note / Doctor's Certificate",
                Filter = "Documents|*.pdf;*.jpg;*.jpeg;*.png;*.bmp|PDF Files|*.pdf|Images|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
            {
                SickNoteFilePath = dialog.FileName;
                HasSickNote = true;
            }
        }

        [RelayCommand]
        private async Task DeleteRecord(object? parameter)
        {
            var row = parameter as AttendanceHistoryRow ?? SelectedItem;
            var record = row?.Record;
            if (record == null) return;
            try
            {
                IsBusy = true;
                await _attendanceService.DeleteAttendanceRecordAsync(record.Id);
                NotifySuccess("Deleted", "Attendance record removed.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attendance record");
                NotifyError("Delete Failed", ex.Message);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditPanelOpen = false;
            EditingRecord = null;
        }
    }

    /// <summary>
    /// View-model row that pairs an <see cref="AttendanceRecord"/> with its
    /// resolved employee name so the DataGrid can bind directly to <c>EmployeeName</c>.
    /// All other properties are forwarded to the underlying record so existing
    /// XAML column bindings continue to work without modification.
    /// </summary>
    public class AttendanceHistoryRow
    {
        public AttendanceRecord Record { get; set; } = null!;

        // Resolved display name (filled by the VM from _employeeNameMap)
        public string EmployeeName { get; set; } = string.Empty;

        // Forwarded record properties — keeps XAML bindings intact
        public DateTime        Date          => Record.Date;
        public AttendanceStatus Status        => Record.Status;
        public DateTime?       CheckInTime    => Record.CheckInTime;
        public DateTime?       CheckOutTime   => Record.CheckOutTime;
        public double          HoursWorked    => Record.HoursWorked;
        public string?         Branch         => Record.Branch;
        public string?         Notes          => Record.Notes;
    }
}
