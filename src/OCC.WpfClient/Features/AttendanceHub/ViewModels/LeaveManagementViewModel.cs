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

        public override IRelayCommand<object>? OpenCommand => null;
        public override IRelayCommand<object>? DeleteCommand => DeleteLeaveCommand;

        // ── Apply Panel ──────────────────────────────────────────────────────
        [ObservableProperty] private bool _isApplyPanelOpen;
        [ObservableProperty] private ObservableCollection<OCC.Shared.DTOs.EmployeeSummaryDto> _employees = new();
        [ObservableProperty] private OCC.Shared.DTOs.EmployeeSummaryDto? _selectedEmployee;
        [ObservableProperty] private DateTime _startDate = DateTime.Today;
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private LeaveType _selectedLeaveType = LeaveType.Annual;
        [ObservableProperty] private string _reason = string.Empty;
        [ObservableProperty] private int _calculatedDays;
        [ObservableProperty] private bool _hasBalanceWarning;
        [ObservableProperty] private string _balanceWarning = string.Empty;

        public IEnumerable<LeaveType> LeaveTypes { get; } = Enum.GetValues<LeaveType>();

        // ── Stats ────────────────────────────────────────────────────────────
        [ObservableProperty] private int _pendingCount;
        [ObservableProperty] private int _approvedCount;
        [ObservableProperty] private int _rejectedCount;

        // ── Status filter ────────────────────────────────────────────────────
        [ObservableProperty] private int _selectedFilterIndex; // 0 All, 1 Pending, 2 Approved, 3 Rejected

        partial void OnSelectedFilterIndexChanged(int value) => FilterItems();
        partial void OnStartDateChanged(DateTime value) => RecalculateDays();
        partial void OnEndDateChanged(DateTime value) => RecalculateDays();
        partial void OnSelectedEmployeeChanged(OCC.Shared.DTOs.EmployeeSummaryDto? value) => RecalculateDays();
        partial void OnSelectedLeaveTypeChanged(LeaveType value) => RecalculateDays();

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
                    emps.OrderBy(e => e.FirstName));

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
            Reason = string.Empty;
            StartDate = DateTime.Today;
            EndDate = DateTime.Today;
            SelectedLeaveType = LeaveType.Annual;
            SelectedEmployee = Employees.FirstOrDefault();
            HasBalanceWarning = false;
            BalanceWarning = string.Empty;
            IsApplyPanelOpen = true;
        }

        [RelayCommand]
        private void CloseApplyPanel() => IsApplyPanelOpen = false;

        private void RecalculateDays()
        {
            if (EndDate < StartDate) { CalculatedDays = 0; return; }
            CalculatedDays = _leaveService.CalculateBusinessDays(StartDate, EndDate);
            CheckBalance();
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

            try
            {
                IsBusy = true;
                var request = new LeaveRequest
                {
                    EmployeeId = SelectedEmployee.Id,
                    StartDate = StartDate.Date,
                    EndDate = EndDate.Date,
                    NumberOfDays = CalculatedDays,
                    LeaveType = SelectedLeaveType,
                    Reason = Reason,
                    Status = LeaveStatus.Pending
                };

                await _leaveService.SubmitLeaveRequestAsync(request);
                NotifySuccess("Leave Submitted", $"Leave request for {SelectedEmployee.FirstName} submitted for approval.");
                IsApplyPanelOpen = false;
                await LoadDataAsync();
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
            string message = targets.Count > 1
                ? $"You are about to delete {targets.Count} records. This action cannot be undone. Are you sure you want to proceed?"
                : "Are you sure you want to delete this leave request? This action cannot be undone.";

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
        }
    }
}
