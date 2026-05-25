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
    public partial class AttendanceHistoryListViewModel : ListViewModelBase<AttendanceRecord>
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IEmployeeService _employeeService;
        private readonly ILogger<AttendanceHistoryListViewModel> _logger;
        private List<AttendanceRecord> _allRecords = new();

        public override string ReportTitle => "Attendance History Report";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Employee ID", PropertyName = "EmployeeId", Width = 1.5 },
            new() { Header = "Date", PropertyName = "Date", Width = 1 },
            new() { Header = "Status", PropertyName = "Status", Width = 1 },
            new() { Header = "Clock In", PropertyName = "CheckInTime", Width = 1 },
            new() { Header = "Clock Out", PropertyName = "CheckOutTime", Width = 1 },
            new() { Header = "Hours", PropertyName = "HoursWorked", Width = 0.8 },
            new() { Header = "Branch", PropertyName = "Branch", Width = 1 },
        };

        public override IRelayCommand<object>? OpenCommand => EditRecordCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteRecordCommand;

        [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _toDate = DateTime.Today;
        [ObservableProperty] private int _selectedBranchIndex = 0;   // 0 = All
        [ObservableProperty] private int _selectedStatusIndex = 0;   // 0 = All
        [ObservableProperty] private int _totalHours;
        [ObservableProperty] private AttendanceRecord? _editingRecord;
        [ObservableProperty] private bool _isEditPanelOpen;

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

            var result = filtered.OrderByDescending(r => r.Date).ToList();
            Items = new ObservableCollection<AttendanceRecord>(result);
            TotalCount = result.Count;
            TotalHours = (int)result.Sum(r => r.HoursWorked);
        }

        public string GetEmployeeName(Guid? id) =>
            id.HasValue && _employeeNameMap.TryGetValue(id.Value, out var n) ? n : "Unknown";

        [RelayCommand]
        private void EditRecord(object? parameter)
        {
            var record = parameter as AttendanceRecord ?? SelectedItem;
            if (record == null) return;
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
                await _attendanceService.UpdateAttendanceRecordAsync(EditingRecord);
                NotifySuccess("Saved", "Attendance record updated.");
                IsEditPanelOpen = false;
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
        private async Task DeleteRecord(object? parameter)
        {
            var record = parameter as AttendanceRecord ?? SelectedItem;
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
}
