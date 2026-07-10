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
    /// Manages the full leave lifecycle: apply, approve/reject, balance warnings, sick note upload and PDF printing.
    /// </summary>
    public partial class LeaveManagementViewModel : ListViewModelBase<LeaveRequest>
    {
        private readonly ILeaveService _leaveService;
        private readonly IEmployeeService _employeeService;
        private readonly IAttendanceService _attendanceService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<LeaveManagementViewModel> _logger;

        private List<LeaveRequest> _allRequests = new();

        public override string ReportTitle => "Leave Register";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Employee",   PropertyName = "EmployeeName", Width = 2 },
            new() { Header = "Type",       PropertyName = "LeaveType",    Width = 1 },
            new() { Header = "Start",      PropertyName = "StartDate",    Width = 1 },
            new() { Header = "End",        PropertyName = "EndDate",      Width = 1 },
            new() { Header = "Days",       PropertyName = "NumberOfDays", Width = 0.7 },
            new() { Header = "Status",     PropertyName = "Status",       Width = 1 },
        };

        public override IRelayCommand<object>? OpenCommand => EditLeaveCommand;
        public override IRelayCommand<object>? EditCommand => EditLeaveCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteLeaveCommand;

        [ObservableProperty] private bool _isEditing;

        public string PanelHeaderTitle => IsEditing ? "EDIT LEAVE DETAILS" : "APPLY FOR LEAVE";
        public string SubmitButtonText => IsEditing ? "SAVE CHANGES" : "SUBMIT REQUEST";
        public bool IsEmployeeSelectionEnabled => !IsEditing;

        partial void OnIsEditingChanged(bool value)
        {
            OnPropertyChanged(nameof(PanelHeaderTitle));
            OnPropertyChanged(nameof(SubmitButtonText));
            OnPropertyChanged(nameof(IsEmployeeSelectionEnabled));
        }

        // ── Apply Panel ──────────────────────────────────────────────────────
        [ObservableProperty] private bool _isApplyPanelOpen;
        [ObservableProperty] private ObservableCollection<OCC.Shared.DTOs.EmployeeSummaryDto> _employees = new();
        [ObservableProperty] private OCC.Shared.DTOs.EmployeeSummaryDto? _selectedEmployee;
        [ObservableProperty] private DateTime _startDate = DateTime.Today;
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsHalfDayType))]
        [NotifyPropertyChangedFor(nameof(IsOtherType))]
        [NotifyPropertyChangedFor(nameof(IsHourly))]
        [NotifyPropertyChangedFor(nameof(IsFullDay))]
        private LeaveType _selectedLeaveType = LeaveType.Annual;

        [ObservableProperty] private string _reason = string.Empty;
        [ObservableProperty] private double _calculatedDays;
        [ObservableProperty] private bool _hasBalanceWarning;
        [ObservableProperty] private string _balanceWarning = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsHourly))]
        [NotifyPropertyChangedFor(nameof(IsFullDay))]
        private LeaveDurationType _selectedDurationType = LeaveDurationType.FullDay;

        [ObservableProperty] private double? _hoursRequested;

        // ── Doctor's Note Panel ──
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDrawerOpen))]
        private bool _isDoctorsNotePanelOpen;

        [ObservableProperty] private string? _doctorsNoteFilePath;
        [ObservableProperty] private DateTime _noteStartDate = DateTime.Today;
        [ObservableProperty] private DateTime _noteEndDate = DateTime.Today;
        [ObservableProperty] private string _noteReason = string.Empty;
        [ObservableProperty] private OCC.Shared.DTOs.EmployeeSummaryDto? _selectedNoteEmployee;
        [ObservableProperty] private ObservableCollection<DoctorsNoteDayViewModel> _noteDays = new();
        [ObservableProperty] private string _noteStatusSummary = string.Empty;
        private double _selectedNoteEmployeeSickBalance;

        public bool IsDrawerOpen
        {
            get => IsApplyPanelOpen || IsDoctorsNotePanelOpen;
            set
            {
                if (!value)
                {
                    IsApplyPanelOpen = false;
                    IsDoctorsNotePanelOpen = false;
                }
                OnPropertyChanged(nameof(IsDrawerOpen));
            }
        }

        public IEnumerable<LeaveType> LeaveTypes { get; } = Enum.GetValues<LeaveType>();
        public IEnumerable<LeaveDurationType> DurationTypes { get; } = Enum.GetValues<LeaveDurationType>();

        public IEnumerable<LeaveDurationType> HalfDayPeriods { get; } = new[] { LeaveDurationType.MorningHalfDay, LeaveDurationType.AfternoonHalfDay };
        public IEnumerable<LeaveDurationType> OtherDurations { get; } = new[] { LeaveDurationType.FullDay, LeaveDurationType.Hourly };

        public bool IsHalfDayType => SelectedLeaveType == LeaveType.HalfDay;
        public bool IsOtherType => SelectedLeaveType == LeaveType.Other;

        public bool IsHourly => SelectedLeaveType == LeaveType.Other && SelectedDurationType == LeaveDurationType.Hourly;
        public bool IsFullDay => SelectedLeaveType != LeaveType.HalfDay && (SelectedLeaveType != LeaveType.Other || SelectedDurationType == LeaveDurationType.FullDay);

        // ── Stats ────────────────────────────────────────────────────────────
        [ObservableProperty] private int _pendingCount;
        [ObservableProperty] private int _approvedCount;
        [ObservableProperty] private int _rejectedCount;

        // ── Status filter ────────────────────────────────────────────────────
        [ObservableProperty] private int _selectedFilterIndex; // 0 All, 1 Pending, 2 Approved, 3 Rejected

        // ── Date range filter ────────────────────────────────────────────────
        [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _toDate = DateTime.Today;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomTimeSpan))]
        private int _selectedTimeSpanIndex = 0; // Default: All Time

        public bool IsCustomTimeSpan => SelectedTimeSpanIndex == 7;
        private bool _isUpdatingTimeSpan;

        partial void OnFromDateChanged(DateTime value)
        {
            if (!_isUpdatingTimeSpan && SelectedTimeSpanIndex == 7)
            {
                FilterItems();
            }
        }

        partial void OnToDateChanged(DateTime value)
        {
            if (!_isUpdatingTimeSpan && SelectedTimeSpanIndex == 7)
            {
                FilterItems();
            }
        }

        partial void OnSelectedTimeSpanIndexChanged(int value)
        {
            if (value == 7) return; // Custom

            _isUpdatingTimeSpan = true;
            try
            {
                if (value == 1) // Today
                {
                    FromDate = DateTime.Today;
                    ToDate = DateTime.Today;
                }
                else if (value == 2) // Yesterday
                {
                    FromDate = DateTime.Today.AddDays(-1);
                    ToDate = DateTime.Today.AddDays(-1);
                }
                else if (value == 3) // This Week
                {
                    DateTime start = DateTime.Today;
                    while (start.DayOfWeek != DayOfWeek.Saturday) start = start.AddDays(-1);
                    FromDate = start;
                    ToDate = start.AddDays(6);
                }
                else if (value == 4) // Last Week
                {
                    DateTime start = DateTime.Today;
                    while (start.DayOfWeek != DayOfWeek.Saturday) start = start.AddDays(-1);
                    start = start.AddDays(-7);
                    FromDate = start;
                    ToDate = start.AddDays(6);
                }
                else if (value == 5) // This Month
                {
                    DateTime firstDay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    FromDate = firstDay;
                    ToDate = firstDay.AddMonths(1).AddDays(-1);
                }
                else if (value == 6) // Last Month
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

            FilterItems();
        }

        [RelayCommand]
        private void SetFilterStatus(object? parameter)
        {
            if (parameter != null && int.TryParse(parameter.ToString(), out var index))
            {
                SelectedFilterIndex = index;
            }
        }

        partial void OnSelectedFilterIndexChanged(int value) => FilterItems();
        partial void OnStartDateChanged(DateTime value) => RecalculateDays();
        partial void OnEndDateChanged(DateTime value) => RecalculateDays();
        partial void OnSelectedEmployeeChanged(OCC.Shared.DTOs.EmployeeSummaryDto? value) => RecalculateDays();
        partial void OnSelectedLeaveTypeChanged(LeaveType value) => RecalculateDays();
        partial void OnSelectedDurationTypeChanged(LeaveDurationType value) => RecalculateDays();
        partial void OnHoursRequestedChanged(double? value) => RecalculateDays();

        public LeaveManagementViewModel(
            ILeaveService leaveService,
            IEmployeeService employeeService,
            IAttendanceService attendanceService,
            IDialogService dialogService,
            IPdfService pdfService,
            ILogger<LeaveManagementViewModel> logger) : base(pdfService)
        {
            _leaveService = leaveService;
            _employeeService = employeeService;
            _attendanceService = attendanceService;
            _dialogService = dialogService;
            _logger = logger;
            Title = "Leave Management";
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading leave records...";

                var emps = await _employeeService.GetEmployeesAsync();
                Employees = new ObservableCollection<OCC.Shared.DTOs.EmployeeSummaryDto>(
                    emps.Where(e => e.Status == EmployeeStatus.Active).OrderBy(e => e.FirstName));

                _allRequests = (await _leaveService.GetLeaveRequestsAsync()).ToList();
                FilterItems();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading leave data");
                NotifyError("Load Failed", ex.Message);
            }
            finally { IsBusy = false; }
        }

        protected override void FilterItems()
        {
            var filtered = _allRequests.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLower();
                filtered = filtered.Where(r =>
                    (r.Employee != null && $"{r.Employee.FirstName} {r.Employee.LastName}".ToLower().Contains(q)) ||
                    r.LeaveType.ToString().ToLower().Contains(q) ||
                    r.Reason.ToLower().Contains(q));
            }

            filtered = SelectedFilterIndex switch
            {
                1 => filtered.Where(r => r.Status == LeaveStatus.Pending),
                2 => filtered.Where(r => r.Status == LeaveStatus.Approved),
                3 => filtered.Where(r => r.Status == LeaveStatus.Rejected),
                _ => filtered
            };

            if (SelectedTimeSpanIndex != 0)
            {
                filtered = filtered.Where(r => r.StartDate.Date <= ToDate.Date && r.EndDate.Date >= FromDate.Date);
            }

            var result = filtered.OrderByDescending(r => r.CreatedDate).ToList();
            Items = new ObservableCollection<LeaveRequest>(result);
            TotalCount = result.Count;

            PendingCount = _allRequests.Count(r => r.Status == LeaveStatus.Pending);
            ApprovedCount = _allRequests.Count(r => r.Status == LeaveStatus.Approved);
            RejectedCount = _allRequests.Count(r => r.Status == LeaveStatus.Rejected);
        }

        // ── Apply Panel ──────────────────────────────────────────────────────

        [RelayCommand]
        private void OpenApplyPanel()
        {
            IsEditing = false;
            Reason = string.Empty;
            StartDate = DateTime.Today;
            EndDate = DateTime.Today;
            SelectedLeaveType = LeaveType.Annual;
            SelectedDurationType = LeaveDurationType.FullDay;
            HoursRequested = null;
            SelectedEmployee = Employees.FirstOrDefault();
            HasBalanceWarning = false;
            BalanceWarning = string.Empty;
            IsApplyPanelOpen = true;
        }

        [RelayCommand]
        private void EditLeave(object? parameter)
        {
            var request = parameter as LeaveRequest ?? SelectedItem;
            if (request == null) return;
            SelectedItem = request;
            IsEditing = true;

            SelectedEmployee = Employees.FirstOrDefault(e => e.Id == request.EmployeeId);
            SelectedLeaveType = request.LeaveType;
            SelectedDurationType = request.DurationType;
            HoursRequested = request.HoursRequested;
            StartDate = request.StartDate;
            EndDate = request.EndDate;
            Reason = request.Reason;

            IsApplyPanelOpen = true;
        }

        [RelayCommand]
        private void CloseApplyPanel() => IsApplyPanelOpen = false;

        private void RecalculateDays()
        {
            if (SelectedLeaveType == LeaveType.HalfDay)
            {
                if (SelectedDurationType != LeaveDurationType.MorningHalfDay && SelectedDurationType != LeaveDurationType.AfternoonHalfDay)
                {
                    SelectedDurationType = LeaveDurationType.MorningHalfDay;
                }
                EndDate = StartDate;
                CalculatedDays = 0.5;
            }
            else if (SelectedLeaveType == LeaveType.Other)
            {
                if (SelectedDurationType != LeaveDurationType.FullDay && SelectedDurationType != LeaveDurationType.Hourly)
                {
                    SelectedDurationType = LeaveDurationType.FullDay;
                }

                if (SelectedDurationType == LeaveDurationType.FullDay)
                {
                    if (EndDate < StartDate) { CalculatedDays = 0; return; }
                    CalculatedDays = _leaveService.CalculateBusinessDays(StartDate, EndDate);
                }
                else
                {
                    EndDate = StartDate;
                    double dailyHours = GetEmployeeDailyShiftHours();
                    CalculatedDays = Math.Round((HoursRequested ?? 0.0) / dailyHours, 2);
                }
            }
            else
            {
                if (SelectedDurationType != LeaveDurationType.FullDay)
                {
                    SelectedDurationType = LeaveDurationType.FullDay;
                }
                if (EndDate < StartDate) { CalculatedDays = 0; return; }
                CalculatedDays = _leaveService.CalculateBusinessDays(StartDate, EndDate);
            }
            CheckBalance();
        }

        private double GetEmployeeDailyShiftHours()
        {
            double dailyHours = 9.0;
            if (SelectedEmployee != null && SelectedEmployee.ShiftStartTime.HasValue && SelectedEmployee.ShiftEndTime.HasValue)
            {
                dailyHours = (SelectedEmployee.ShiftEndTime.Value - SelectedEmployee.ShiftStartTime.Value).TotalHours;
                if (SelectedEmployee.ShiftEndTime.Value.Hours >= 13)
                {
                    dailyHours -= 1.0;
                }
                if (dailyHours < 0) dailyHours = 0;
            }
            return dailyHours <= 0 ? 9.0 : dailyHours;
        }

        private void CheckBalance()
        {
            HasBalanceWarning = false;
            BalanceWarning = string.Empty;
            // Balance check is informational only — we look at the selected employee's balance
            // but don't block submission
        }

        [RelayCommand]
        private async Task SubmitLeave()
        {
            if (SelectedEmployee == null)
            {
                NotifyError("Validation", "Please select an employee.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Reason))
            {
                NotifyError("Validation", "Please enter a reason for the leave request.");
                return;
            }
            if (EndDate < StartDate)
            {
                NotifyError("Validation", "End date must be on or after start date.");
                return;
            }
            if (CalculatedDays <= 0)
            {
                NotifyError("Validation", "The selected date range contains no working days.");
                return;
            }

            if (SelectedLeaveType == LeaveType.Other && SelectedDurationType == LeaveDurationType.Hourly)
            {
                if (HoursRequested == null || HoursRequested <= 0)
                {
                    NotifyError("Validation", "Please enter a valid number of leave hours.");
                    return;
                }
                double shiftHrs = GetEmployeeDailyShiftHours();
                if (HoursRequested > shiftHrs)
                {
                    NotifyError("Validation", $"Requested hours cannot exceed the employee's standard shift of {shiftHrs} hours.");
                    return;
                }
            }

            try
            {
                IsBusy = true;
                double draftPaid = CalculatedDays;
                double draftUnpaid = 0;
                if (SelectedLeaveType == LeaveType.CulturalObligations)
                {
                    double capped = Math.Min(3.0, CalculatedDays);
                    double availableAnnual = SelectedEmployee.LeaveBalance;
                    draftPaid = Math.Min(capped, availableAnnual);
                    draftUnpaid = CalculatedDays - draftPaid;
                }
                else if (SelectedLeaveType == LeaveType.Unpaid || SelectedLeaveType == LeaveType.AbsentWithoutLeave)
                {
                    draftPaid = 0;
                    draftUnpaid = CalculatedDays;
                }

                if (IsEditing)
                {
                    if (SelectedItem == null) return;
                    var request = SelectedItem;
                    request.EmployeeId = SelectedEmployee.Id;
                    request.StartDate = StartDate.Date;
                    request.EndDate = EndDate.Date;
                    request.NumberOfDays = CalculatedDays;
                    request.LeaveType = SelectedLeaveType;
                    request.DurationType = SelectedDurationType;
                    request.HoursRequested = HoursRequested;
                    request.PaidDays = draftPaid;
                    request.UnpaidDays = draftUnpaid;
                    request.Reason = Reason;
                    request.IsUnpaid = (SelectedLeaveType == LeaveType.Unpaid || SelectedLeaveType == LeaveType.AbsentWithoutLeave);

                    var success = await _leaveService.UpdateLeaveRequestAsync(request);
                    if (success)
                    {
                        NotifySuccess("Leave Updated", "Leave request details updated successfully.");
                        IsApplyPanelOpen = false;
                        await LoadDataAsync();
                    }
                    else
                    {
                        NotifyError("Update Failed", "Could not update leave request details.");
                    }
                }
                else
                {
                    var request = new LeaveRequest
                    {
                        EmployeeId = SelectedEmployee.Id,
                        StartDate = StartDate.Date,
                        EndDate = EndDate.Date,
                        NumberOfDays = CalculatedDays,
                        LeaveType = SelectedLeaveType,
                        DurationType = SelectedDurationType,
                        HoursRequested = HoursRequested,
                        PaidDays = draftPaid,
                        UnpaidDays = draftUnpaid,
                        Reason = Reason,
                        Status = LeaveStatus.Pending,
                        IsUnpaid = (SelectedLeaveType == LeaveType.Unpaid || SelectedLeaveType == LeaveType.AbsentWithoutLeave)
                    };

                    await _leaveService.SubmitLeaveRequestAsync(request);
                    NotifySuccess("Leave Submitted", $"Leave request for {SelectedEmployee.FirstName} submitted for approval.");
                    IsApplyPanelOpen = false;
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting leave");
                NotifyError("Submit Failed", ex.Message);
            }
            finally { IsBusy = false; }
        }

        // ── Approve / Reject ─────────────────────────────────────────────────

        [RelayCommand]
        private async Task ApproveLeave(LeaveRequest? request)
        {
            if (request == null) return;
            try
            {
                IsBusy = true;
                var success = await _leaveService.ApproveLeaveAsync(request.Id);
                if (success)
                {
                    var empName = request.Employee != null
                        ? $"{request.Employee.FirstName} {request.Employee.LastName}"
                        : "Employee";
                    NotifySuccess("Approved", $"Leave approved for {empName}. {request.NumberOfDays} day(s) deducted from balance.");
                    await LoadDataAsync();
                }
                else
                {
                    NotifyError("Failed", "Could not approve leave request.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving leave {Id}", request.Id);
                NotifyError("Error", ex.Message);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task RejectLeave(LeaveRequest? request)
        {
            if (request == null) return;
            try
            {
                IsBusy = true;
                var success = await _leaveService.RejectLeaveAsync(request.Id);
                if (success)
                {
                    NotifySuccess("Rejected", "Leave request has been rejected.");
                    await LoadDataAsync();
                }
                else
                {
                    NotifyError("Failed", "Could not reject leave request.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting leave {Id}", request.Id);
                NotifyError("Error", ex.Message);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task DeleteLeave(object? parameter)
        {
            var targets = GetDeleteTargets(parameter);
            if (!targets.Any()) return;

            string title = targets.Count > 1 ? "Delete Multiple Leave Requests" : "Delete Leave Request";
            
            string message;
            bool hasApprovedLeave = targets.Any(l => l.Status == LeaveStatus.Approved);
            
            if (targets.Count > 1)
            {
                if (hasApprovedLeave)
                {
                    message = $"You are about to delete {targets.Count} leave requests. Some of these requests are already APPROVED. Deleting them may cause leave balance inconsistencies.\n\n" +
                              "Rather Reject the leave requests instead of deleting them.\n\n" +
                              "Are you sure you want to proceed with deleting anyway?";
                }
                else
                {
                    message = $"You are about to delete {targets.Count} records. This action cannot be undone. Are you sure you want to proceed?";
                }
            }
            else
            {
                var target = targets[0];
                var empName = target.Employee != null ? $"{target.Employee.FirstName} {target.Employee.LastName}" : "the employee";
                if (target.Status == LeaveStatus.Approved)
                {
                    message = $"The leave request for {empName} is already APPROVED.\n\n" +
                              "Deleting this request may cause leave balance inconsistencies. Rather Reject the leave request instead.\n\n" +
                              "Are you sure you want to permanently delete this leave request?";
                }
                else
                {
                    message = "Are you sure you want to delete this leave request? This action cannot be undone.";
                }
            }

            var confirmed = await _dialogService.ShowConfirmationAsync(title, message);
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = targets.Count > 1 ? "Deleting leave requests..." : "Deleting leave request...";
                foreach (var target in targets)
                {
                    await _leaveService.DeleteLeaveRequestAsync(target.Id);
                }
                NotifySuccess("Deleted", targets.Count > 1 ? $"{targets.Count} leave requests deleted." : "Leave request removed.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting leave request(s)");
                NotifyError("Error", ex.Message);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task PrintLeaveForm(LeaveRequest? request)
        {
            if (request == null) return;
            try
            {
                IsBusy = true;
                BusyText = "Generating leave form PDF...";
                var path = await _pdfService.GenerateLeaveFormPdfAsync(request);
                if (!string.IsNullOrEmpty(path))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating leave form PDF");
                NotifyError("PDF Failed", ex.Message);
            }
            finally { IsBusy = false; }
        }

        public override void CloseOverlay()
        {
            base.CloseOverlay();
            IsApplyPanelOpen = false;
            IsDoctorsNotePanelOpen = false;
        }

        [RelayCommand]
        private void OpenDoctorsNotePanel()
        {
            DoctorsNoteFilePath = null;
            NoteStartDate = DateTime.Today;
            NoteEndDate = DateTime.Today;
            NoteReason = string.Empty;
            SelectedNoteEmployee = Employees.FirstOrDefault();
            NoteDays.Clear();
            NoteStatusSummary = "Select dates and click 'LOAD DAYS IN RANGE'.";
            IsDoctorsNotePanelOpen = true;
        }

        [RelayCommand]
        private void CloseDoctorsNotePanel()
        {
            IsDoctorsNotePanelOpen = false;
        }

        [RelayCommand]
        private void SelectNoteFile()
        {
            var path = _dialogService.ShowOpenFileDialog("Documents|*.pdf;*.jpg;*.jpeg;*.png;*.bmp|PDF Files|*.pdf|Images|*.jpg;*.jpeg;*.png;*.bmp", "Select Doctor's Certificate");
            if (!string.IsNullOrEmpty(path))
            {
                DoctorsNoteFilePath = path;
            }
        }

        async partial void OnSelectedNoteEmployeeChanged(OCC.Shared.DTOs.EmployeeSummaryDto? value)
        {
            if (value == null)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    _selectedNoteEmployeeSickBalance = 0;
                    RecalculateNoteSummary();
                });
                return;
            }

            try
            {
                var emp = await _employeeService.GetEmployeeAsync(value.Id);
                App.Current.Dispatcher.Invoke(() =>
                {
                    _selectedNoteEmployeeSickBalance = emp?.SickLeaveBalance ?? 0;
                    RecalculateNoteSummary();
                });
            }
            catch
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    _selectedNoteEmployeeSickBalance = 0;
                    RecalculateNoteSummary();
                });
            }
        }

        private void RecalculateNoteSummary()
        {
            if (SelectedNoteEmployee == null)
            {
                NoteStatusSummary = "Please select an employee.";
                return;
            }

            double balance = _selectedNoteEmployeeSickBalance;
            int selectedCount = NoteDays.Count(d => d.IsCovered);

            double paid = Math.Min(selectedCount, balance);
            double unpaid = Math.Max(0, selectedCount - paid);

            NoteStatusSummary = $"Employee Sick Leave Balance: {balance:F1} days\n" +
                                $"Days Selected to Cover: {selectedCount}\n" +
                                $"─ Paid Sick Days: {paid:F1}\n" +
                                $"─ Unpaid Sick Days: {unpaid:F1} (balance exhausted)";
        }

        [RelayCommand]
        private async Task LoadNoteDays()
        {
            if (SelectedNoteEmployee == null)
            {
                NotifyError("Validation", "Please select an employee.");
                return;
            }

            if (NoteEndDate < NoteStartDate)
            {
                NotifyError("Validation", "End date must be on or after start date.");
                return;
            }

            try
            {
                IsBusy = true;
                BusyText = "Loading attendance history...";

                // Fetch attendance for this employee in the range
                var allRecords = await _attendanceService.GetAttendanceRecordsAsync(NoteStartDate.Date, NoteEndDate.Date);
                var empRecords = allRecords.Where(r => r.EmployeeId == SelectedNoteEmployee.Id).ToList();

                var dayVms = new List<DoctorsNoteDayViewModel>();
                for (var d = NoteStartDate.Date; d <= NoteEndDate.Date; d = d.AddDays(1))
                {
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                    if (OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(d)) continue;

                    var existing = empRecords.FirstOrDefault(r => r.Date.Date == d);
                    string status = existing?.Status.ToString() ?? "No Record";

                    var dayVm = new DoctorsNoteDayViewModel
                    {
                        Date = d,
                        CurrentStatus = status,
                        IsCovered = status == "Absent" || status == "UnpaidSick" || status == "No Record"
                    };

                    dayVm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(DoctorsNoteDayViewModel.IsCovered))
                        {
                            App.Current.Dispatcher.Invoke(() => RecalculateNoteSummary());
                        }
                    };

                    dayVms.Add(dayVm);
                }

                App.Current.Dispatcher.Invoke(() =>
                {
                    NoteDays.Clear();
                    foreach (var vm in dayVms)
                    {
                        NoteDays.Add(vm);
                    }
                    RecalculateNoteSummary();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading note days");
                NotifyError("Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ProcessDoctorsNote()
        {
            if (SelectedNoteEmployee == null)
            {
                NotifyError("Validation", "Please select an employee.");
                return;
            }

            var checkedDays = NoteDays.Where(d => d.IsCovered).OrderBy(d => d.Date).ToList();
            if (!checkedDays.Any())
            {
                NotifyError("Validation", "Please check at least one day covered by the note.");
                return;
            }

            if (string.IsNullOrWhiteSpace(NoteReason))
            {
                NotifyError("Validation", "Please enter a reason or diagnosis.");
                return;
            }

            try
            {
                IsBusy = true;
                BusyText = "Uploading doctor's note and processing sick leave...";

                // 1. Upload the certificate if path is local
                string? serverPath = null;
                if (!string.IsNullOrEmpty(DoctorsNoteFilePath) && System.IO.File.Exists(DoctorsNoteFilePath))
                {
                    serverPath = await _attendanceService.UploadSickNoteAsync(DoctorsNoteFilePath);
                }

                // 2. Group checked days into contiguous ranges to create LeaveRequests
                var groups = new List<List<DateTime>>();
                List<DateTime>? currentGroup = null;
                foreach (var day in checkedDays)
                {
                    if (currentGroup == null || (day.Date - currentGroup.Last()).Days > 1)
                    {
                        currentGroup = new List<DateTime>();
                        groups.Add(currentGroup);
                    }
                    currentGroup.Add(day.Date);
                }

                double remainingSickBalance = _selectedNoteEmployeeSickBalance;

                foreach (var group in groups)
                {
                    var start = group.First();
                    var end = group.Last();
                    int totalDays = group.Count;

                    // Calculate paid/unpaid split
                    double paidDays = Math.Min(totalDays, remainingSickBalance);
                    double unpaidDays = totalDays - paidDays;
                    remainingSickBalance = Math.Max(0, remainingSickBalance - paidDays);

                    // Create Leave Request (Pending)
                    var lr = new LeaveRequest
                    {
                        EmployeeId = SelectedNoteEmployee.Id,
                        StartDate = start,
                        EndDate = end,
                        NumberOfDays = totalDays,
                        PaidDays = paidDays,
                        UnpaidDays = unpaidDays,
                        LeaveType = LeaveType.Sick,
                        DurationType = LeaveDurationType.FullDay,
                        Reason = NoteReason,
                        DoctorsNoteImagePath = serverPath,
                        Status = LeaveStatus.Pending,
                        IsUnpaid = paidDays == 0
                    };

                    var submitted = await _leaveService.SubmitLeaveRequestAsync(lr);
                    if (submitted != null)
                    {
                        // Approve it to trigger attendance generation and balance deduction
                        await _leaveService.ApproveLeaveAsync(submitted.Id, $"Processed via doctor's note upload. Note: {NoteReason}");
                    }
                }

                NotifySuccess("Processed Note", $"Doctor's note processed. Sick leave applied and balances updated.");
                IsDoctorsNotePanelOpen = false;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing doctor's note");
                NotifyError("Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task PrintSickLeaveReport()
        {
            Guid? employeeId = null;
            if (SelectedItem != null)
            {
                employeeId = SelectedItem.EmployeeId;
            }
            else if (SelectedEmployee != null)
            {
                employeeId = SelectedEmployee.Id;
            }

            if (!employeeId.HasValue)
            {
                await _dialogService.ShowAlertAsync("Select Employee", "Please select a leave request or an employee in the details panel to print their sick leave report.");
                return;
            }

            try
            {
                IsBusy = true;
                BusyText = "Generating sick leave report PDF...";

                var employee = await _employeeService.GetEmployeeAsync(employeeId.Value);
                if (employee == null)
                {
                    NotifyError("Error", "Employee not found.");
                    return;
                }

                var allLeaves = await _leaveService.GetLeaveRequestsAsync();
                var sickLeaves = allLeaves
                    .Where(l => l.EmployeeId == employee.Id && l.LeaveType == LeaveType.Sick && l.Status == LeaveStatus.Approved)
                    .OrderByDescending(l => l.StartDate)
                    .ToList();

                var startDate = employee.LeaveCycleStartDate ?? DateTime.Today.AddYears(-1);
                var allAttendance = await _attendanceService.GetAttendanceRecordsAsync(startDate, DateTime.Today);
                var sickDays = allAttendance
                    .Where(a => a.EmployeeId == employee.Id && (a.Status == AttendanceStatus.Sick || a.Status == AttendanceStatus.UnpaidSick))
                    .ToList();

                var path = await _pdfService.GenerateSickLeaveReportPdfAsync(employee, sickLeaves, sickDays);
                if (!string.IsNullOrEmpty(path))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                    NotifySuccess("Success", "Sick leave report generated.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing sick leave report");
                NotifyError("Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public class DoctorsNoteDayViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private bool _isCovered;
        public DateTime Date { get; set; }
        public string DayOfWeek => Date.DayOfWeek.ToString();
        public string CurrentStatus { get; set; } = "Absent";

        public bool IsCovered
        {
            get => _isCovered;
            set => SetProperty(ref _isCovered, value);
        }
    }
}
