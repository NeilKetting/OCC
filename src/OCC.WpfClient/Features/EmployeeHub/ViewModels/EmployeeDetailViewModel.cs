using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Features.EmployeeHub.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.EmployeeHub.ViewModels
{
    public partial class EmployeeDetailViewModel : DetailViewModelBase
    {
        private readonly EmployeeListViewModel _parent;
        private readonly IEmployeeService _employeeService;

        [ObservableProperty]
        private EmployeeModel _employee;

        [ObservableProperty]
        private bool _isNew;

        public List<EmployeeRole> Roles { get; } = new(Enum.GetValues<EmployeeRole>());
        public List<EmploymentType> EmploymentTypes { get; } = new(Enum.GetValues<EmploymentType>());
        public List<EmployeeStatus> Statuses { get; } = new(Enum.GetValues<EmployeeStatus>());
        public List<IdType> IdTypes { get; } = new(Enum.GetValues<IdType>());
        public List<string> Branches { get; } = new() { "Johannesburg", "Cape Town" };
        public List<string> AccountTypes { get; } = new() { "Savings", "Cheque", "Transmission" };
        public List<BankName> Banks { get; } = new(Enum.GetValues<BankName>());

        [ObservableProperty]
        private List<User> _availableUsers = new();

        [ObservableProperty]
        private string _leaveAccrualRule = "Standard: 15 Working Days Annual / 30 Days Sick Leave Cycle";

        [ObservableProperty]
        private double _currentAnnualLeaveBalance;

        [ObservableProperty]
        private string _sickLeaveCycleEndDisplay = "N/A";

        public bool IsPassportVisible => Employee.IdType == IdType.RSAId;
        public bool IsContractVisible => Employee.EmploymentType == EmploymentType.Contract;

        private readonly IUserService _userService;
        private readonly IAuthService _authService;

        public EmployeeDetailViewModel(EmployeeListViewModel parent, EmployeeModel employee, IEmployeeService employeeService, IUserService userService, IAuthService authService, IDialogService dialogService, ILogger logger) : base(dialogService, logger)
        {
            _parent = parent;
            Employee = employee;
            _employeeService = employeeService;
            _userService = userService;
            _authService = authService;
            _isNew = employee.Id == Guid.Empty;
            
            Title = _isNew ? "Add Employee" : $"Edit {employee.DisplayName}";

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var users = await _userService.GetUsersAsync();
                AvailableUsers = users.OrderBy(u => u.DisplayName).ToList();

                UpdateAccrualRule();
                UpdateSickLeaveCycleEnd();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users for employee link");
            }
        }

        partial void OnEmployeeChanged(EmployeeModel value)
        {
            if (value != null)
            {
                value.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(EmployeeModel.IdNumber))
                    {
                        if (Employee.IdType == IdType.RSAId)
                            CalculateDoBFromRsaId(Employee.IdNumber);
                    }
                    else if (e.PropertyName == nameof(EmployeeModel.IdType))
                    {
                        OnPropertyChanged(nameof(IsPassportVisible));
                        if (Employee.IdType == IdType.RSAId)
                            CalculateDoBFromRsaId(Employee.IdNumber);
                    }
                    else if (e.PropertyName == nameof(EmployeeModel.Branch))
                    {
                        UpdateShiftTimes();
                    }
                    else if (e.PropertyName == nameof(EmployeeModel.Role))
                    {
                    }
                    else if (e.PropertyName == nameof(EmployeeModel.EmploymentType))
                    {
                        OnPropertyChanged(nameof(IsContractVisible));
                        UpdateAccrualRule();
                    }
                    else if (e.PropertyName == nameof(EmployeeModel.LeaveCycleStartDate))
                    {
                        UpdateSickLeaveCycleEnd();
                    }
                    else if (e.PropertyName == nameof(EmployeeModel.LinkedUserId))
                    {
                        HandleLinkedUserChange(Employee.LinkedUserId);
                    }
                    else if (e.PropertyName == nameof(EmployeeModel.FirstName) || e.PropertyName == nameof(EmployeeModel.LastName))
                    {
                        OnPropertyChanged(nameof(Title));
                    }
                };
            }
        }

        private void CalculateDoBFromRsaId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length < 6) return;
            string datePart = id.Substring(0, 6);
            
            if (DateTime.TryParseExact(datePart, "yyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime dob))
            {
                // Simple assumption for century (current window is 1920-2019)
                if (dob > DateTime.Now) dob = dob.AddYears(-100);
                Employee.DoB = dob;
            }
        }

        private void UpdateShiftTimes()
        {
            var jhbStart = new TimeSpan(7, 0, 0);
            var jhbEnd = new TimeSpan(16, 45, 0);
            var cptStart = new TimeSpan(7, 0, 0);
            var cptEnd = new TimeSpan(16, 30, 0);

            if (string.Equals(Employee.Branch, "Johannesburg", StringComparison.OrdinalIgnoreCase))
            {
                Employee.ShiftStartTime = jhbStart;
                Employee.ShiftEndTime = jhbEnd;
            }
            else if (string.Equals(Employee.Branch, "Cape Town", StringComparison.OrdinalIgnoreCase))
            {
                Employee.ShiftStartTime = cptStart;
                Employee.ShiftEndTime = cptEnd;
            }
        }

        private void UpdateAccrualRule()
        {
            if (Employee.EmploymentType == EmploymentType.Contract)
            {
                LeaveAccrualRule = "Accrual: 1 day / 17 days (Annual) | 1 day / 26 days (Sick)";
                if (IsNew && Employee.AnnualLeaveBalance == 0)
                {
                    Employee.AnnualLeaveBalance = 0;
                    Employee.SickLeaveBalance = 0;
                }
            }
            else
            {
                LeaveAccrualRule = "Standard: 15 Working Days Annual / 30 Days Sick Leave Cycle";
                if (IsNew && Employee.AnnualLeaveBalance == 0)
                {
                    Employee.SickLeaveBalance = 30;
                }
            }
        }

        private void UpdateSickLeaveCycleEnd()
        {
            if (Employee.LeaveCycleStartDate.HasValue && Employee.LeaveCycleStartDate.Value > new DateTime(1900, 1, 1))
            {
                var endDate = Employee.LeaveCycleStartDate.Value.AddMonths(36).AddDays(-1);
                SickLeaveCycleEndDisplay = endDate.ToString("dd MMM yyyy");
            }
            else
            {
                SickLeaveCycleEndDisplay = "N/A";
            }
        }

        private void HandleLinkedUserChange(Guid? userId)
        {
            if (userId.HasValue)
            {
                var selectedUser = AvailableUsers.FirstOrDefault(u => u.Id == userId.Value);
                if (selectedUser != null)
                {
                    if (string.IsNullOrWhiteSpace(Employee.FirstName)) Employee.FirstName = selectedUser.FirstName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(Employee.LastName)) Employee.LastName = selectedUser.LastName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(Employee.Email)) Employee.Email = selectedUser.Email ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(Employee.Phone)) Employee.Phone = selectedUser.Phone ?? string.Empty;
                }
            }
        }

        protected override async Task<bool> ValidateAsync()
        {
            ValidationErrors.Clear();
            Employee.Validate();
            if (Employee.HasErrors)
            {
                foreach (var err in Employee.GetErrors())
                {
                    ValidationErrors.Add(err.ErrorMessage ?? "Validation error");
                }
                HasErrors = true;
                await PulseValidationAsync();
                return false;
            }
            HasErrors = false;
            return true;
        }

        protected override async Task ExecuteSaveAsync()
        {

            if (IsNew)
            {
                var result = await _employeeService.CreateEmployeeAsync(Employee.ToEntity());
                if (result == null) throw new Exception("Failed to create employee.");
            }
            else
            {
                var success = await _employeeService.UpdateEmployeeAsync(Employee.ToEntity());
                if (!success) throw new Exception("Failed to update employee.");
            }
        }

        protected override void OnSaveSuccess()
        {
            NotifySuccess("Success", $"Employee '{Employee.DisplayName}' saved successfully.");
            _parent.LoadDataAsync().ConfigureAwait(false);
            _parent.CloseDetailView();
        }

        protected override async Task<bool> ExecuteForceSaveAsync()
        {
            try
            {
                var latest = await _employeeService.GetEmployeeAsync(Employee.Id);
                if (latest != null)
                {
                    // Update ONLY the RowVersion to bypass the concurrency check
                    Employee.RowVersion = latest.RowVersion;
                    await ExecuteSaveAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during force save");
                NotifyError("Force Save Error", "Could not complete the force save: " + ex.Message);
            }
            return false;
        }

        protected override async Task ExecuteReloadAsync()
        {
            var latestDto = await _employeeService.GetEmployeeAsync(Employee.Id);
            if (latestDto != null)
            {
                Employee.UpdateFromEntity(latestDto);
                Title = $"Edit {Employee.DisplayName} (Reloaded)";
            }
        }

        protected override void OnCancel()
        {
            _parent.CloseDetailView();
        }

        [RelayCommand]
        private void Print()
        {
            _logger.LogInformation("Print Profile requested for {DisplayName}", Employee.DisplayName);
        }
    }
}
