using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class ManualAttendanceViewModel : ViewModelBase
    {
        private readonly ITimeService _timeService;
        private readonly IDialogService _dialogService;

        // We want to default to Yesterday since Manual Attendance is strictly for past dates
        [ObservableProperty]
        private DateTime _selectedDate = DateTime.Today.AddDays(-1);

        public DateTime MaxDate => DateTime.Today.AddDays(-1);

        [ObservableProperty]
        private TimeSpan _clockInTime = new TimeSpan(8, 0, 0);

        [ObservableProperty]
        private TimeSpan _clockOutTime = new TimeSpan(17, 0, 0);

        [ObservableProperty]
        private string _selectedBranch = "All";

        public ObservableCollection<string> BranchOptions { get; } = new ObservableCollection<string>
        {
            "All",
            "Johannesburg",
            "Cape Town"
        };

        [ObservableProperty]
        private string _selectedPayType = "All";

        public ObservableCollection<string> PayTypeOptions { get; } = new ObservableCollection<string>
        {
            "All",
            "Hourly",
            "Salary"
        };

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<SelectableEmployeeViewModel> _employees = new();

        private List<SelectableEmployeeViewModel> _allEmployeeViewModels = new();

        private bool _isUpdatingFilters;

        [ObservableProperty]
        private bool _isAllSelected;

        public ManualAttendanceViewModel(ITimeService timeService, IDialogService dialogService)
        {
            _timeService = timeService;
            _dialogService = dialogService;
        }

        [RelayCommand]
        public async Task LoadEmployees()
        {
            IsBusy = true;
            try
            {
                var staff = await _timeService.GetAllStaffAsync();
                _allEmployeeViewModels = staff.Select(e => new SelectableEmployeeViewModel(e)).ToList();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to load employees: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ApplyFilters()
        {
            var filtered = _allEmployeeViewModels.AsEnumerable();

            if (SelectedBranch != "All")
            {
                filtered = filtered.Where(e => e.Branch == SelectedBranch);
            }

            if (SelectedPayType != "All")
            {
                filtered = filtered.Where(e => e.Employee.RateType.ToString() == SelectedPayType || 
                                              (SelectedPayType == "Salary" && e.Employee.RateType == RateType.MonthlySalary));
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(e => e.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // Bring selected employees to the top, then sort alphabetically
            filtered = filtered.OrderByDescending(e => e.IsSelected).ThenBy(e => e.DisplayName);

            _isUpdatingFilters = true;
            Employees = new ObservableCollection<SelectableEmployeeViewModel>(filtered);
            IsAllSelected = Employees.Any() && Employees.All(e => e.IsSelected);
            _isUpdatingFilters = false;
        }

        partial void OnSelectedBranchChanged(string value) => ApplyFilters();
        partial void OnSelectedPayTypeChanged(string value) => ApplyFilters();
        partial void OnSearchTextChanged(string value) => ApplyFilters();

        partial void OnIsAllSelectedChanged(bool value)
        {
            if (_isUpdatingFilters) return;

            foreach (var emp in Employees)
            {
                emp.IsSelected = value;
            }
        }

        [RelayCommand]
        private async Task CommitAttendance()
        {
            var selectedEmployees = _allEmployeeViewModels.Where(e => e.IsSelected).ToList();
            if (!selectedEmployees.Any())
            {
                await _dialogService.ShowAlertAsync("No Selection", "Please select at least one employee.");
                return;
            }

            var recordDate = SelectedDate.Date;

            if (recordDate >= DateTime.Today)
            {
                await _dialogService.ShowAlertAsync("Invalid Date", "Manual attendance can only be captured for past dates.");
                return;
            }

            IsBusy = true;
            try
            {
                // Check existing records for the day
                var existingRecords = await _timeService.GetDailyAttendanceAsync(recordDate);
                var existingEmployeeIds = existingRecords.Select(r => r.EmployeeId).ToHashSet();
                
                var alreadyRecorded = selectedEmployees.Where(e => existingEmployeeIds.Contains(e.Employee.Id)).ToList();
                if (alreadyRecorded.Any())
                {
                    IsBusy = false;
                    var names = string.Join(", ", alreadyRecorded.Select(e => e.DisplayName).Take(3));
                    if (alreadyRecorded.Count > 3) names += $" and {alreadyRecorded.Count - 3} others";
                    await _dialogService.ShowAlertAsync("Existing Records", $"The following employees already have attendance records for {recordDate:dd MMM yyyy}:\n\n{names}\n\nPlease deselect them or edit their records in the History view.");
                    return;
                }

                bool confirm = await _dialogService.ShowConfirmationAsync("Confirm Action", 
                    $"Are you sure you want to add attendance for {selectedEmployees.Count} employees for {recordDate:dd MMM yyyy}?");

                if (!confirm) 
                {
                    IsBusy = false;
                    return;
                }
                var checkIn = recordDate.Add(ClockInTime);
                var checkOut = recordDate.Add(ClockOutTime);
                
                // Handle midnight crossover if necessary (though usually In < Out for same day)
                if (checkOut < checkIn) checkOut = checkOut.AddDays(1);

                var tasks = selectedEmployees.Select(e => 
                {
                    var record = new AttendanceRecord
                    {
                        EmployeeId = e.Employee.Id,
                        Date = recordDate,
                        CheckInTime = checkIn,
                        CheckOutTime = checkOut,
                        Status = AttendanceStatus.Present,
                        Branch = e.Employee.Branch ?? ""
                    };
                    return _timeService.SaveAttendanceRecordAsync(record);
                });

                await Task.WhenAll(tasks);
                
                await _dialogService.ShowAlertAsync("Success", $"Attendance records created for {selectedEmployees.Count} employees.");
                
                // Clear selection
                foreach (var emp in _allEmployeeViewModels) emp.IsSelected = false;
                IsAllSelected = false;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to save attendance: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public partial class SelectableEmployeeViewModel : ObservableObject
    {
        public Employee Employee { get; }

        [ObservableProperty]
        private bool _isSelected;

        public string DisplayName => Employee.DisplayName;
        public string Branch => Employee.Branch ?? "N/A";
        public string Role => Employee.Role.ToString();

        public SelectableEmployeeViewModel(Employee employee)
        {
            Employee = employee;
        }
    }
}
