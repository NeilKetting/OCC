using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System.Collections.ObjectModel;
using System.IO;
using ExcelDataReader;

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
        private readonly IProjectService _projectService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<AttendanceHistoryListViewModel> _logger;
        private List<AttendanceRecord> _allRecords = new();

        public override string ReportTitle => "Attendance History Report";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Employee",   PropertyName = "EmployeeName",  Width = 1.8 },
            new() { Header = "Project",    PropertyName = "ProjectName",   Width = 2 },
            new() { Header = "Date",       PropertyName = "Date",          Width = 1 },
            new() { Header = "Status",     PropertyName = "Status",        Width = 1 },
            new() { Header = "Clock In",   PropertyName = "CheckInTime",   Width = 1 },
            new() { Header = "Clock Out",  PropertyName = "CheckOutTime",  Width = 1 },
            new() { Header = "Hours",      PropertyName = "HoursWorked",   Width = 0.8 },
            new() { Header = "Branch",     PropertyName = "Branch",        Width = 1 },
        };

        public override IRelayCommand<object>? OpenCommand => EditRecordCommand;
        public override IRelayCommand<object>? EditCommand => EditRecordCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteRecordCommand;

        [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _toDate = DateTime.Today;
        [ObservableProperty] private int _selectedBranchIndex = 0;
        [ObservableProperty] private int _selectedStatusIndex = 0;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomTimeSpan))]
        private int _selectedTimeSpanIndex = 6; // Default: Custom (updated for This Month option)

        [ObservableProperty] private int _selectedRateTypeIndex = 0; // Default: All
        [ObservableProperty] private int _totalHours;
        [ObservableProperty] private double _drawerWidth = 380.0;

        public bool IsCustomTimeSpan => SelectedTimeSpanIndex == 6;
        private bool _isUpdatingTimeSpan;
        
        [ObservableProperty] private bool _isDateColumnVisible = true;
        [ObservableProperty] private bool _isEmployeeColumnVisible = true;
        [ObservableProperty] private bool _isProjectColumnVisible = true;
        [ObservableProperty] private bool _isStatusColumnVisible = true;
        [ObservableProperty] private bool _isClockInColumnVisible = true;
        [ObservableProperty] private bool _isClockOutColumnVisible = true;
        [ObservableProperty] private bool _isHoursColumnVisible = true;
        [ObservableProperty] private bool _isBranchColumnVisible = true;
        [ObservableProperty] private bool _isNotesColumnVisible = true;

        // Rich employee name lookup for display
        private Dictionary<Guid, string> _employeeNameMap = new();
        private Dictionary<Guid, RateType> _employeeRateTypeMap = new();
        private Dictionary<Guid, string> _projectNameMap = new();

        public AttendanceHistoryListViewModel(
            IAttendanceService attendanceService,
            IEmployeeService employeeService,
            IProjectService projectService,
            IDialogService dialogService,
            IPdfService pdfService,
            ILogger<AttendanceHistoryListViewModel> logger) : base(pdfService)
        {
            _attendanceService = attendanceService;
            _employeeService = employeeService;
            _projectService = projectService;
            _dialogService = dialogService;
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
                _employeeRateTypeMap = employees.ToDictionary(e => e.Id, e => e.RateType);

                // Build project name map for display
                var projects = await _projectService.GetProjectSummariesAsync(includeDeleted: true);
                _projectNameMap = projects.ToDictionary(p => p.Id, p => p.Name);

                DateTime? from = null;
                DateTime? to = null;
                if (SelectedTimeSpanIndex != 0) // Not "All"
                {
                    from = FromDate;
                    to = ToDate;
                }

                _allRecords = (await _attendanceService.GetAttendanceRecordsAsync(from, to)).ToList();
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

        partial void OnFromDateChanged(DateTime value)
        {
            if (!_isUpdatingTimeSpan && SelectedTimeSpanIndex == 6)
            {
                _ = LoadDataAsync();
            }
        }

        partial void OnToDateChanged(DateTime value)
        {
            if (!_isUpdatingTimeSpan && SelectedTimeSpanIndex == 6)
            {
                _ = LoadDataAsync();
            }
        }

        partial void OnSelectedBranchIndexChanged(int value) => FilterItems();
        partial void OnSelectedStatusIndexChanged(int value) => FilterItems();
        partial void OnSelectedRateTypeIndexChanged(int value) => FilterItems();

        partial void OnSelectedTimeSpanIndexChanged(int value)
        {
            if (value == 6) return; // Custom

            _isUpdatingTimeSpan = true;
            try
            {
                if (value == 1) // Today
                {
                    FromDate = DateTime.Today;
                    ToDate = DateTime.Today;
                }
                else if (value == 2) // This Week
                {
                    DateTime start = DateTime.Today;
                    while (start.DayOfWeek != DayOfWeek.Saturday) start = start.AddDays(-1);
                    FromDate = start;
                    ToDate = start.AddDays(6);
                }
                else if (value == 3) // Last Week
                {
                    DateTime start = DateTime.Today;
                    while (start.DayOfWeek != DayOfWeek.Saturday) start = start.AddDays(-1);
                    start = start.AddDays(-7);
                    FromDate = start;
                    ToDate = start.AddDays(6);
                }
                else if (value == 4) // This Month
                {
                    DateTime firstDay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    FromDate = firstDay;
                    ToDate = firstDay.AddMonths(1).AddDays(-1);
                }
                else if (value == 5) // Last Month
                {
                    DateTime firstDay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
                    FromDate = firstDay;
                    ToDate = firstDay.AddMonths(1).AddDays(-1);
                }
            }
            finally
            {
                _isUpdatingTimeSpan = false;
            }

            _ = LoadDataAsync();
        }

        protected override void FilterItems()
        {
            var filtered = _allRecords.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLower();
                filtered = filtered.Where(r =>
                    (r.Branch?.ToLower().Contains(q) ?? false) ||
                    (r.EmployeeId.HasValue && _employeeNameMap.TryGetValue(r.EmployeeId.Value, out var name) && name.ToLower().Contains(q)) ||
                    (r.ProjectId.HasValue && _projectNameMap.TryGetValue(r.ProjectId.Value, out var projName) && projName.ToLower().Contains(q)) ||
                    (r.CustomSite != null && r.CustomSite.ToLower().Contains(q)) ||
                    (r.Notes != null && r.Notes.ToLower().Contains(q) && (r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late || r.Status == AttendanceStatus.LeaveEarly)));
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

            filtered = SelectedRateTypeIndex switch
            {
                1 => filtered.Where(r => r.EmployeeId.HasValue && _employeeRateTypeMap.TryGetValue(r.EmployeeId.Value, out var rt) && rt == RateType.Hourly),
                2 => filtered.Where(r => r.EmployeeId.HasValue && _employeeRateTypeMap.TryGetValue(r.EmployeeId.Value, out var rt) && rt == RateType.MonthlySalary),
                _ => filtered
            };

            var result = filtered
                .Select(r => new AttendanceHistoryRow
                {
                    Record       = r,
                    EmployeeName = GetEmployeeName(r.EmployeeId),
                    ProjectName  = GetProjectName(r)
                })
                .OrderBy(r => r.EmployeeName)
                .ThenByDescending(r => r.Date)
                .ToList();
            Items = new ObservableCollection<AttendanceHistoryRow>(result);
            TotalCount = result.Count;
            TotalHours = (int)result.Sum(r => r.Record.HoursWorked);
        }

        public string GetEmployeeName(Guid? id) =>
            id.HasValue && _employeeNameMap.TryGetValue(id.Value, out var n) ? n : "Unknown";

        public string GetProjectName(AttendanceRecord r)
        {
            if (r.ProjectId.HasValue && _projectNameMap.TryGetValue(r.ProjectId.Value, out var n))
                return n;

            if (!string.IsNullOrEmpty(r.CustomSite))
                return r.CustomSite;

            if (r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late || r.Status == AttendanceStatus.LeaveEarly)
            {
                // Fallback for legacy records, filtering out auto clock-in system notes
                if (!string.IsNullOrEmpty(r.Notes) && 
                    !r.Notes.Contains("Auto Clock-In", StringComparison.OrdinalIgnoreCase) && 
                    !r.Notes.Contains("Auto Clock-Out", StringComparison.OrdinalIgnoreCase) && 
                    !r.Notes.Contains("generated by system", StringComparison.OrdinalIgnoreCase))
                {
                    return r.Notes;
                }
            }

            if (r.IsAutoClockIn || (!string.IsNullOrEmpty(r.Notes) && (
                r.Notes.Contains("Auto Clock-In", StringComparison.OrdinalIgnoreCase) || 
                r.Notes.Contains("Auto Clock-Out", StringComparison.OrdinalIgnoreCase) || 
                r.Notes.Contains("generated by system", StringComparison.OrdinalIgnoreCase))))
            {
                return string.Empty;
            }

            return "Office / General";
        }

        [RelayCommand]
        private void AddRecord()
        {
            var record = new AttendanceRecord
            {
                Id = Guid.Empty,
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
                if (res != null) // Meaning saved successfully
                {
                    await LoadDataAsync();
                }
            });
        }

        [RelayCommand]
        private async Task ImportExcel()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Weekly Biometric Sheet (Excel)",
                Filter = "Excel Files|*.xlsx;*.xls;*.xlsb",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            var filePath = dialog.FileName;
            var parsedRows = new List<TempImportRow>();

            try
            {
                IsBusy = true;
                BusyText = "Reading Excel sheet...";

                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                await Task.Run(() =>
                {
                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var reader = ExcelReaderFactory.CreateReader(stream))
                        {
                            var result = reader.AsDataSet();
                            if (result == null) return;

                            foreach (System.Data.DataTable table in result.Tables)
                            {
                                // Skip metadata/empty sheets
                                if (table.Rows.Count < 8 || table.Columns.Count < 7) continue;

                                // Let's check row 3 (0-indexed) for dates
                                var dates = new Dictionary<int, DateTime>();
                                for (int col = 2; col < table.Columns.Count; col += 5)
                                {
                                    if (col >= table.Columns.Count) break;
                                    var cellVal = table.Rows[3][col];
                                    if (cellVal is DateTime dt)
                                    {
                                        dates[col] = dt;
                                    }
                                    else if (cellVal != null && cellVal != DBNull.Value && DateTime.TryParse(cellVal.ToString(), out var parsedDt))
                                    {
                                        dates[col] = parsedDt;
                                    }
                                }

                                if (dates.Count == 0) continue;

                                // Parse employees starting from row 8, stepping by 2
                                for (int r = 8; r < table.Rows.Count; r += 2)
                                {
                                    var row = table.Rows[r];
                                    var empName = row[0]?.ToString()?.Trim();
                                    if (string.IsNullOrEmpty(empName)) continue;
                                    if (empName.StartsWith("OCC :", StringComparison.OrdinalIgnoreCase) || 
                                        empName.StartsWith("TOTAL", StringComparison.OrdinalIgnoreCase)) continue;

                                    // Parse 7 days (Saturday to Friday)
                                    for (int col = 2; col < table.Columns.Count; col += 5)
                                    {
                                        if (!dates.TryGetValue(col, out var date)) continue;

                                        var site = row[col]?.ToString()?.Trim() ?? "";
                                        var checkInVal = row[col + 1];
                                        var checkOutVal = row[col + 2];

                                        if (string.IsNullOrEmpty(site) && checkInVal == DBNull.Value && checkOutVal == DBNull.Value)
                                        {
                                            // No clocking for this day
                                            continue;
                                        }

                                        TimeSpan? checkIn = null;
                                        TimeSpan? checkOut = null;

                                        if (checkInVal is DateTime cInDt) checkIn = cInDt.TimeOfDay;
                                        else if (checkInVal != null && checkInVal != DBNull.Value && TimeSpan.TryParse(checkInVal.ToString(), out var cInTs)) checkIn = cInTs;

                                        if (checkOutVal is DateTime cOutDt) checkOut = cOutDt.TimeOfDay;
                                        else if (checkOutVal != null && checkOutVal != DBNull.Value && TimeSpan.TryParse(checkOutVal.ToString(), out var cOutTs)) checkOut = cOutTs;

                                        parsedRows.Add(new TempImportRow
                                        {
                                            RawEmployeeName = empName,
                                            RawSiteName = site,
                                            Date = date,
                                            CheckInTime = checkIn,
                                            CheckOutTime = checkOut
                                        });
                                    }
                                }
                            }
                        }
                    }
                });

                if (parsedRows.Count == 0)
                {
                    NotifyError("No Records Found", "Could not find any valid daily attendance entries in the selected Excel sheet.");
                    return;
                }

                var employees = await _employeeService.GetEmployeesAsync();
                var projects = await _projectService.GetProjectSummariesAsync(includeDeleted: false);

                var previewVm = new AttendanceImportPreviewViewModel(
                    parsedRows,
                    employees.ToList(),
                    projects.ToList(),
                    _attendanceService
                );

                // Dynamically expand the drawer for the preview grid!
                DrawerWidth = 1300.0;

                OpenOverlay(previewVm, async (res) =>
                {
                    // Restore original drawer width when overlay closes
                    DrawerWidth = 380.0;

                    if (res is int count && count > 0)
                    {
                        NotifySuccess("Import Completed", $"Successfully imported {count} attendance records.");
                        await LoadDataAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing Excel biometric sheet");
                NotifyError("Import Failed", $"Failed to parse Excel file: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task AssignProject(object? parameter)
        {
            var targets = new List<AttendanceHistoryRow>();
            if (parameter is System.Collections.IList list)
            {
                foreach (var item in list)
                {
                    if (item is AttendanceHistoryRow r)
                        targets.Add(r);
                }
            }
            else if (parameter is AttendanceHistoryRow row)
            {
                targets.Add(row);
            }
            else if (SelectedItem != null)
            {
                targets.Add(SelectedItem);
            }

            if (!targets.Any()) return;

            // Fetch list of projects for selection
            var projects = await _projectService.GetProjectSummariesAsync(includeDeleted: false);
            var pList = new List<OCC.Shared.DTOs.ProjectSummaryDto>
            {
                new OCC.Shared.DTOs.ProjectSummaryDto { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "-- Please Select a Site --" },
                new OCC.Shared.DTOs.ProjectSummaryDto { Id = Guid.Empty, Name = "Other (Specify)..." }
            };
            pList.AddRange(projects.OrderBy(p => p.Name));

            // Open dialog
            var result = await _dialogService.ShowAssignProjectDialogAsync(pList);
            if (result == null) return; // Cancelled

            var selectedProjId = result.Value.ProjectId;
            var customSite = result.Value.CustomSite;

            try
            {
                IsBusy = true;
                BusyText = "Assigning project...";

                foreach (var target in targets)
                {
                    var record = target.Record;
                    record.ProjectId = selectedProjId;
                    record.CustomSite = customSite;

                    await _attendanceService.UpdateAttendanceRecordAsync(record);
                }

                NotifySuccess("Assigned", targets.Count > 1 ? $"{targets.Count} records assigned to project." : "Record assigned to project.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning project to attendance record(s)");
                NotifyError("Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void EditRecord(object? parameter)
        {
            var row = parameter as AttendanceHistoryRow ?? SelectedItem;
            var record = row?.Record;
            if (record == null) return;

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
                if (res != null) // Meaning saved successfully
                {
                    await LoadDataAsync();
                }
            });
        }

        [RelayCommand]
        private async Task DeleteRecord(object? parameter)
        {
            var targets = GetDeleteTargets(parameter);
            if (!targets.Any()) return;

            string title = targets.Count > 1 ? "Delete Multiple Records" : "Delete Attendance Record";
            string message = targets.Count > 1
                ? $"You are about to delete {targets.Count} records. This action cannot be undone. Are you sure you want to proceed?"
                : $"Are you sure you want to delete the attendance record for '{targets[0].EmployeeName}'? This action cannot be undone.";

            var confirmed = await _dialogService.ShowConfirmationAsync(title, message);
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = targets.Count > 1 ? "Deleting records..." : "Deleting record...";
                foreach (var target in targets)
                {
                    await _attendanceService.DeleteAttendanceRecordAsync(target.Record.Id);
                }
                NotifySuccess("Deleted", targets.Count > 1 ? $"{targets.Count} records deleted." : "Attendance record removed.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attendance record(s)");
                NotifyError("Delete Failed", ex.Message);
            }
            finally { IsBusy = false; }
        }

        public override async Task PrintAsync()
        {
            if (Items == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Generating weekly report...";

                // Determine all Saturday-to-Friday weeks within the FromDate to ToDate range
                // A week starts on Saturday. Find the Saturday on or before FromDate.
                DateTime startSat = FromDate.Date;
                while (startSat.DayOfWeek != DayOfWeek.Saturday)
                {
                    startSat = startSat.AddDays(-1);
                }

                var weeksList = new List<WeeklyAttendanceReportWeekModel>();

                // Load all active employees and filter/sort them
                var activeEmps = (await _employeeService.GetEmployeesAsync())
                    .Where(e => e.Status == EmployeeStatus.Active);

                if (SelectedBranchIndex == 1)
                {
                    activeEmps = activeEmps.Where(e => e.Branch == "Johannesburg");
                }
                else if (SelectedBranchIndex == 2)
                {
                    activeEmps = activeEmps.Where(e => e.Branch == "Cape Town");
                }

                if (SelectedRateTypeIndex == 1)
                {
                    activeEmps = activeEmps.Where(e => e.RateType == RateType.Hourly);
                }
                else if (SelectedRateTypeIndex == 2)
                {
                    activeEmps = activeEmps.Where(e => e.RateType == RateType.MonthlySalary);
                }

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    var q = SearchQuery.ToLower();
                    activeEmps = activeEmps.Where(e =>
                        $"{e.FirstName} {e.LastName}".ToLower().Contains(q) ||
                        (e.EmployeeNumber != null && e.EmployeeNumber.ToLower().Contains(q)) ||
                        (e.Branch != null && e.Branch.ToLower().Contains(q)));
                }

                var sortedEmployees = activeEmps
                    .OrderBy(e => $"{e.FirstName} {e.LastName}".Trim())
                    .ToList();

                // Build weeks
                for (DateTime weekStart = startSat; weekStart <= ToDate.Date; weekStart = weekStart.AddDays(7))
                {
                    DateTime weekEnd = weekStart.AddDays(6);

                    var weekModel = new WeeklyAttendanceReportWeekModel
                    {
                        WeekStart = weekStart,
                        WeekEnd = weekEnd,
                        FilterFromDate = FromDate,
                        FilterToDate = ToDate
                    };

                    // Get all attendance records in Items that fall in this week
                    var weekRecords = Items.Where(r => r.Date.Date >= weekStart && r.Date.Date <= weekEnd).ToList();

                    // Find if there are any other employee names in these week records that aren't in our active list
                    var weekEmployeeNames = weekRecords.Select(r => r.EmployeeName).Distinct().ToList();

                    // Combine active sorted employees and any extra employees with records this week
                    var allWeekEmployees = new List<string>();
                    foreach (var emp in sortedEmployees)
                    {
                        allWeekEmployees.Add($"{emp.FirstName} {emp.LastName}".Trim());
                    }
                    foreach (var extraName in weekEmployeeNames)
                    {
                        if (!allWeekEmployees.Any(name => name.Equals(extraName, StringComparison.OrdinalIgnoreCase)))
                        {
                            allWeekEmployees.Add(extraName);
                        }
                    }

                    // Sort the combined list alphabetically
                    allWeekEmployees = allWeekEmployees.OrderBy(name => name).ToList();

                    foreach (var empName in allWeekEmployees)
                    {
                        var empRecords = weekRecords.Where(r => r.EmployeeName.Equals(empName, StringComparison.OrdinalIgnoreCase)).ToList();

                        var empModel = new WeeklyAttendancePrintModel
                        {
                            EmployeeName = empName
                        };

                        // Populate 7 days Saturday (0) to Friday (6)
                        for (int i = 0; i < 7; i++)
                        {
                            DateTime dayDate = weekStart.AddDays(i);
                            var dayPrint = new DailyAttendancePrintModel();

                            // ONLY grab attendance records for that timespan (between FromDate and ToDate)
                            if (dayDate >= FromDate.Date && dayDate <= ToDate.Date)
                            {
                                var dayRow = empRecords.FirstOrDefault(r => r.Date.Date == dayDate);
                                if (dayRow != null)
                                {
                                    if (dayRow.Status == AttendanceStatus.Absent)
                                    {
                                        dayPrint.Site = "ABSENT";
                                        dayPrint.TimeIn = "XXXX";
                                        dayPrint.TimeOut = "XXXX";
                                        dayPrint.Overtime = "UNP";
                                    }
                                    else if (dayRow.Status == AttendanceStatus.Sick)
                                    {
                                        dayPrint.Site = "ABSENT -SICK";
                                        dayPrint.TimeIn = "XXXX";
                                        dayPrint.TimeOut = "XXXX";
                                        dayPrint.Overtime = "PAID";
                                    }
                                    else if (dayRow.Status == AttendanceStatus.UnpaidSick)
                                    {
                                        dayPrint.Site = "ABSENT -SICK";
                                        dayPrint.TimeIn = "XXXX";
                                        dayPrint.TimeOut = "XXXX";
                                        dayPrint.Overtime = "UNP";
                                    }
                                    else if (dayRow.Status == AttendanceStatus.LeaveAuthorized)
                                    {
                                        dayPrint.Site = "LEAVE";
                                        dayPrint.TimeIn = "XXXX";
                                        dayPrint.TimeOut = "XXXX";
                                        dayPrint.Overtime = "PAID";
                                    }
                                    else // Present, Late, LeaveEarly
                                    {
                                        dayPrint.Site = dayRow.ProjectName ?? string.Empty;
                                        dayPrint.TimeIn = dayRow.CheckInTime?.ToString("HH:mm") ?? string.Empty;
                                        dayPrint.TimeOut = dayRow.CheckOutTime?.ToString("HH:mm") ?? string.Empty;

                                        // Overtime calculation
                                        if (dayDate.DayOfWeek == DayOfWeek.Saturday || dayDate.DayOfWeek == DayOfWeek.Sunday)
                                        {
                                            dayPrint.Overtime = dayRow.HoursWorked > 0 ? dayRow.HoursWorked.ToString("F1") : string.Empty;
                                        }
                                        else
                                        {
                                            dayPrint.Overtime = dayRow.HoursWorked > 8.75 ? (dayRow.HoursWorked - 8.75).ToString("F1") : string.Empty;
                                        }
                                    }
                                }
                            }
                            empModel.Days[i] = dayPrint;
                        }
                        weekModel.Employees.Add(empModel);
                    }

                    if (weekModel.Employees.Any())
                    {
                        weeksList.Add(weekModel);
                    }
                }

                // Call PDF Service
                string branchStr = SelectedBranchIndex switch
                {
                    1 => "Johannesburg",
                    2 => "Cape Town",
                    _ => "All Branches"
                };

                var path = await _pdfService.GenerateWeeklyAttendanceReportPdfAsync(
                    "Weekly Attendance Register",
                    branchStr,
                    SearchQuery,
                    weeksList);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating weekly attendance PDF");
                NotifyError("Print Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
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

        // Resolved project name (filled by the VM from _projectNameMap)
        public string ProjectName { get; set; } = string.Empty;

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
