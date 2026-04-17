using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class LeaveApplicationViewModel : ViewModelBase
    {
        private readonly ILeaveService _leaveService;
        private readonly IRepository<Employee> _employeeRepository;
        private readonly INotificationService _notificationService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<Employee> _employees = new();

        [ObservableProperty]
        private Employee? _selectedEmployee;

        [ObservableProperty]
        private DateTime? _startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime? _endDate = DateTime.Today;

        [ObservableProperty]
        private LeaveType _selectedLeaveType = LeaveType.Annual;
        
        public ObservableCollection<LeaveType> LeaveTypes { get; } = new(Enum.GetValues<LeaveType>());

        [ObservableProperty]
        private string _reason = string.Empty;



        private System.Collections.Generic.List<Employee> _allEmployees = new();

        [ObservableProperty]
        private int _calculatedDays;

        [ObservableProperty]
        private bool _isCalculated;

        [ObservableProperty]
        private string _balanceWarning = string.Empty;

        [ObservableProperty]
        private bool _hasBalanceWarning;

        [ObservableProperty]
        private bool _isSubmitting;

        public LeaveApplicationViewModel(
            ILeaveService leaveService,
            IRepository<Employee> employeeRepository,
            INotificationService notificationService,
            IDialogService dialogService)
        {
            _leaveService = leaveService;
            _employeeRepository = employeeRepository;
            _notificationService = notificationService;
            _dialogService = dialogService;
            
            LoadDataCommand.Execute(null);
        }

        // Parameterless constructor for designer
        public LeaveApplicationViewModel() 
        {
             _leaveService = null!;
             _employeeRepository = null!;
             _notificationService = null!;
             _dialogService = null!;
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
             try
             {
                 var emps = await _employeeRepository.GetAllAsync();
                 // Sort by Name
                 _allEmployees = emps.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList();
                 Employees = new ObservableCollection<Employee>(_allEmployees);
                 
                 if (Employees.Any()) SelectedEmployee = Employees.First();
             }
             catch (Exception ex)
             {
                 System.Diagnostics.Debug.WriteLine($"[LeaveApplicationViewModel] Error loading employees: {ex.Message}");
                 if (_dialogService != null) await _dialogService.ShowAlertAsync("Error", $"Critical Error loading employees: {ex.Message}");
             }
        }

        partial void OnStartDateChanged(DateTime? value) => RecalculateDaysCommand.Execute(null);
        partial void OnEndDateChanged(DateTime? value) => RecalculateDaysCommand.Execute(null);
        partial void OnSelectedEmployeeChanged(Employee? value) => RecalculateDaysCommand.Execute(null);
        partial void OnSelectedLeaveTypeChanged(LeaveType value) => RecalculateDaysCommand.Execute(null);


        [RelayCommand]
        private async Task RecalculateDaysAsync()
        {
            if (StartDate == null || EndDate == null) 
            {
                CalculatedDays = 0;
                IsCalculated = false;
                HasBalanceWarning = false;
                return;
            }

            var start = StartDate.Value.Date;
            var end = EndDate.Value.Date;

            CalculatedDays = await _leaveService.CalculateBusinessDaysAsync(start, end);
            IsCalculated = true;
            
            await CheckBalanceAsync();
        }

        private async Task CheckBalanceAsync()
        {
            HasBalanceWarning = false;
            BalanceWarning = string.Empty;

            if (SelectedEmployee == null) return;
            if (SelectedLeaveType != LeaveType.Annual) return; // Only check Annual Leave balance for now

            double currentBalance = await _leaveService.CalculateCurrentLeaveBalanceAsync(SelectedEmployee.Id);

            if (CalculatedDays > currentBalance)
            {
                double shortfall = CalculatedDays - currentBalance;
                HasBalanceWarning = true;
                BalanceWarning = $"Insufficient Leave Balance ({currentBalance:N2} days available). {shortfall:N2} days will be UNPAID.";
            }
        }

        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (SelectedEmployee == null)
            {
                await _notificationService.SendReminderAsync("Error", "Select an employee.");
                return;
            }
            if (SelectedLeaveType == LeaveType.Annual && SelectedEmployee.LeaveBalance < 0) 
            {
                // Balance warning already shown, but maybe block? User didn't say block.
            }
            
            if (StartDate == null || EndDate == null)
            {
                 await _notificationService.SendReminderAsync("Error", "Select both start and end dates.");
                 return;
            }
            
            if (EndDate < StartDate)
            {
                await _notificationService.SendReminderAsync("Error", "End date must be after start date.");
                return;
            }

            IsSubmitting = true;
            try
            {
                var request = new LeaveRequest
                {
                    EmployeeId = SelectedEmployee.Id,
                    StartDate = StartDate.Value.Date,
                    EndDate = EndDate.Value.Date,
                    LeaveType = SelectedLeaveType,
                    Reason = Reason,
                    IsUnpaid = HasBalanceWarning // Assuming IsUnpaid is determined by HasBalanceWarning
                };

                await _leaveService.SubmitRequestAsync(request);

                await _notificationService.SendReminderAsync("Success", "Leave Request Submitted Successfully.");
                
                // Notify approvals to refresh
                WeakReferenceMessenger.Default.Send(new EntityChangedMessage<LeaveRequest>(request, EntityChangeType.Created));
                
                // Reset form
                Reason = string.Empty;
                StartDate = DateTime.Today;
                EndDate = DateTime.Today;
            }
            catch (Exception ex)
            {
                await _notificationService.SendReminderAsync("Error", $"Failed to submit request: {ex.Message}");
            }
            finally
            {
                IsSubmitting = false;
            }
        }
    }
}
