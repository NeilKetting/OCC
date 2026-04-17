using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services;
using OCC.Shared.Models;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ModelWrappers;
using OCC.Shared.DTOs;
using OCC.Client.ViewModels.Core;

using OCC.Client.Features.TimeAttendanceHub.ViewModels;

namespace OCC.Client.Features.EmployeeHub.ViewModels
{
    public partial class EmployeeDetailViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IEmployeeService _employeeService; // Changed from Repository
        private readonly IRepository<User> _userRepository;
        private readonly IDialogService _dialogService;
        private readonly IAuthService _authService;
        private readonly ILeaveService _leaveService;
        private readonly IExportService _exportService;
        private Guid? _existingStaffId;
        private DateTime _calculatedDoB = DateTime.Now.AddYears(-30);


        #endregion

        #region Events

        public event EventHandler? CloseRequested;
        public event EventHandler? EmployeeAdded;

        #endregion

        #region Observables

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        private EmployeeWrapper _wrapper = null!;

        [ObservableProperty]
        private string _saveButtonText = "Add Employee";

        [ObservableProperty]
        private List<string> _availableAccountTypes = new() { "Select Account Type", "Savings", "Cheque", "Transmission" };

        [ObservableProperty]
        private double _currentAnnualLeaveBalance;

        [ObservableProperty]
        private string _sickLeaveCycleEndDisplay = "N/A";

        [ObservableProperty]
        private List<User> _availableUsers = new();

        [ObservableProperty]
        private string? _currentPermissions;

        [ObservableProperty]
        private bool _isPermissionsPopupVisible;

        [ObservableProperty]
        private EmployeePermissionsViewModel? _permissionsPopupVM;

        [ObservableProperty]
        private bool _showPermissionsButton;

        public bool HasLinkedUser => Wrapper.LinkedUserId.HasValue;

        [ObservableProperty]
        private string _leaveAccrualRule = "Standard: 15 Working Days Annual / 30 Days Sick Leave Cycle";

        [ObservableProperty]
        private BankName _selectedBank = OCC.Shared.Models.BankName.None;

        [ObservableProperty]
        private string _customBankName = string.Empty;

        public bool IsOtherBankSelected => SelectedBank == BankName.Other;

        public BankName[] AvailableBanks { get; } = Enum.GetValues<BankName>();

        partial void OnSelectedBankChanged(BankName value) => OnPropertyChanged(nameof(IsOtherBankSelected));

        #endregion

        #region Properties

        public bool IsHourly
        {
            get => Wrapper.RateType == RateType.Hourly;
            set
            {
                if (value) Wrapper.RateType = RateType.Hourly;
                OnPropertyChanged(nameof(IsHourly));
                OnPropertyChanged(nameof(IsSalary));
            }
        }

        public bool IsSalary
        {
            get => Wrapper.RateType == RateType.MonthlySalary;
            set
            {
                if (value) Wrapper.RateType = RateType.MonthlySalary;
                OnPropertyChanged(nameof(IsHourly));
                OnPropertyChanged(nameof(IsSalary));
            }
        }
        
        public bool IsRsaId
        {
            get => Wrapper.IdType == IdType.RSAId;
            set
            {
                if (value) Wrapper.IdType = IdType.RSAId;
                OnPropertyChanged(nameof(IsRsaId));
                OnPropertyChanged(nameof(IsPassport));
            }
        }

        public bool IsPassport
        {
            get => Wrapper.IdType == IdType.Passport;
            set
            {
                if (value) Wrapper.IdType = IdType.Passport;
                OnPropertyChanged(nameof(IsRsaId));
                OnPropertyChanged(nameof(IsPassport));
            }
        }

        public bool IsPermanent
        {
            get => Wrapper.EmploymentType == EmploymentType.Permanent;
            set
            {
                if (value) Wrapper.EmploymentType = EmploymentType.Permanent;
                OnPropertyChanged(nameof(IsPermanent));
                OnPropertyChanged(nameof(IsContract));
                OnPropertyChanged(nameof(IsContractVisible));
            }
        }

        public bool IsContract
        {
            get => Wrapper.EmploymentType == EmploymentType.Contract;
            set
            {
                if (value) Wrapper.EmploymentType = EmploymentType.Contract;
                OnPropertyChanged(nameof(IsPermanent));
                OnPropertyChanged(nameof(IsContract));
                OnPropertyChanged(nameof(IsContractVisible));
            }
        }

        public bool IsContractVisible => IsContract;

        public bool IsActive
        {
            get => Wrapper.Status == EmployeeStatus.Active;
            set
            {
                Wrapper.Status = value ? EmployeeStatus.Active : EmployeeStatus.Inactive;
                OnPropertyChanged(nameof(IsActive));
            }
        }

        [ObservableProperty]
        private bool _isSystemAccessVisible;

        // Exposed Enum Values for ComboBox
        public List<EmployeeRole> EmployeeRoles { get; } = Enum.GetValues<EmployeeRole>()
            .Where(r => r != EmployeeRole.LegacySeniorForeman && r != EmployeeRole.Supervisor)
            .OrderBy(r => r.ToString())
            .ToList();

        public List<string> Branches { get; } = new() { "Johannesburg", "Cape Town" };

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Wrapper.FirstName) && string.IsNullOrWhiteSpace(Wrapper.LastName))
                {
                    return _existingStaffId.HasValue ? "Edit Employee" : "New Employee";
                }
                return $"{Wrapper.FirstName}, {Wrapper.LastName}".Trim();
            }
        }

        #endregion

        #region Constructors

        public EmployeeDetailViewModel(IEmployeeService employeeService, IRepository<User> userRepository, IDialogService dialogService, IAuthService authService, ILeaveService leaveService, IExportService exportService)
        {
            _employeeService = employeeService;
            _userRepository = userRepository;
            _dialogService = dialogService;
            _authService = authService;
            _leaveService = leaveService;
            _exportService = exportService;
            
            // Initial empty wrapper to prevent null reference checks before Load is called
            Wrapper = new EmployeeWrapper(new Employee());
        }

        public virtual async Task InitializeAsync()
        {
            try
            {
                var users = await _userRepository.GetAllAsync();
                AvailableUsers = users.OrderBy(u => u.DisplayName).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading users: {ex.Message}");
            }
        }

        public virtual async Task InitializeForNew()
        {
            await InitializeAsync();
            // Reset wrapper for new entry
            Wrapper = new EmployeeWrapper(new Employee());
        }

        public EmployeeDetailViewModel() 
        {
            // _employeeService and _dialogService will be null, handle in Save
            _employeeService = null!;
            _userRepository = null!;
            _dialogService = null!;
            _authService = null!;
            _authService = null!;
            _leaveService = null!;
            _exportService = null!;
        }

        #endregion

        #region Commands

        [RelayCommand(CanExecute = nameof(CanSave))]
        public virtual async Task Save()
        {
            Wrapper.Validate();
            if (Wrapper.HasErrors) return;
            // 1. Duplicate Checks - Use Summaries for efficiency
            var allStaffSummaries = await _employeeService.GetEmployeesAsync();
            var otherStaff = _existingStaffId.HasValue 
                ? allStaffSummaries.Where(s => s.Id != _existingStaffId.Value).ToList() 
                : allStaffSummaries.ToList();

            if (!string.IsNullOrWhiteSpace(Wrapper.IdNumber) && otherStaff.Any(s => s.IdNumber == Wrapper.IdNumber))
            {
                 await _dialogService.ShowAlertAsync("Validation Error", $"An employee with ID Number '{Wrapper.IdNumber}' already exists.");
                 return;
            }

            if (!string.IsNullOrWhiteSpace(Wrapper.EmployeeNumber) && otherStaff.Any(s => s.EmployeeNumber == Wrapper.EmployeeNumber))
            {
                 await _dialogService.ShowAlertAsync("Validation Error", $"An employee with Number '{Wrapper.EmployeeNumber}' already exists.");
                 return;
            }

            if (!string.IsNullOrWhiteSpace(Wrapper.Email) && otherStaff.Any(s => s.Email != null && s.Email.Equals(Wrapper.Email, StringComparison.OrdinalIgnoreCase)))
            {
                 await _dialogService.ShowAlertAsync("Validation Error", $"An employee with Email '{Wrapper.Email}' already exists.");
                 return;
            }

            // Validation: Unique User Link
            if (Wrapper.LinkedUserId.HasValue)
            {
                var existingLink = otherStaff.FirstOrDefault(s => s.LinkedUserId == Wrapper.LinkedUserId.Value);
                if (existingLink != null)
                {
                    await _dialogService.ShowAlertAsync("Validation Error", $"The selected user account is already linked to employee '{existingLink.DisplayName}'. An account can only be linked to one employee.");
                    return;
                }

                // Validation: Name Matching
                var selectedUser = AvailableUsers.FirstOrDefault(u => u.Id == Wrapper.LinkedUserId.Value);
                if (selectedUser != null)
                {
                    bool firstMatch = string.Equals(Wrapper.FirstName?.Trim(), selectedUser.FirstName?.Trim(), StringComparison.OrdinalIgnoreCase);
                    bool lastMatch = string.Equals(Wrapper.LastName?.Trim(), selectedUser.LastName?.Trim(), StringComparison.OrdinalIgnoreCase);

                    if (!firstMatch || !lastMatch)
                    {
                        var result = await _dialogService.ShowConfirmationAsync("Name Mismatch", 
                            $"The employee name ({Wrapper.FirstName} {Wrapper.LastName}) does not match the linked account name ({selectedUser.FirstName} {selectedUser.LastName}).\n\nDo you want to proceed anyway?");
                        
                        if (!result) return;
                    }
                }
            }

            // 2. Sync UI-Only properties to the Wrapper/Model
            // Wrapper.DoB is now the source of truth (bound to UI for Passports, auto-calc for RSA)
            
            // Sync Banking details from ViewModel properties to Wrapper
            if (SelectedBank == OCC.Shared.Models.BankName.None)
            {
                 Wrapper.BankName = null;
            }
            else
            {
                 Wrapper.BankName = IsOtherBankSelected ? CustomBankName : GetEnumDescription(SelectedBank);
            }

            // Sync Account Type
            Wrapper.AccountType = (Wrapper.AccountType == "Select Account Type") ? null : Wrapper.AccountType;

            // 3. Commit ALL changes from Wrapper to the underlying Model
            Wrapper.CommitToModel();
            
            // The model to save is the one inside the wrapper
            var staffToSave = Wrapper.Model;

            // 4. Permissions Sync
            if (Wrapper.LinkedUserId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(Wrapper.LinkedUserId.Value);
                if (user != null)
                {
                    user.Permissions = CurrentPermissions;
                    await _userRepository.UpdateAsync(user);
                }
            }

            // 5. Persist to Repository
            try 
            {
                BusyText = "Saving employee details...";
                IsBusy = true;
                
                if (_existingStaffId.HasValue)
                {
                    await _employeeService.UpdateEmployeeAsync(staffToSave);
                }
                else
                {
                    await _employeeService.CreateEmployeeAsync(staffToSave);
                }
                
                EmployeeAdded?.Invoke(this, EventArgs.Empty);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OCC.Client.Infrastructure.Exceptions.ConcurrencyException cex)
            {
                await _dialogService.ShowAlertAsync("Concurrency Conflict", cex.Message);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to save employee: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanSave() => Wrapper != null && !Wrapper.HasErrors;

        [RelayCommand]
        private void Cancel()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void SetSelectedBank(BankName bank)
        {
            SelectedBank = bank;
        }

        [RelayCommand]
        public virtual async Task Print()
        {
            if (Wrapper == null || _exportService == null) return;
            
            // Generate profile based on current wrapper data (even if not saved yet, or partially edited)
            // But usually print reflects saved state. However, user might want to print draft.
            // We'll use Wrapper.Model but ensure sync first?
            // Wrapper.CommitToModel() modifies the underlying model instance. 
            // If we want to print *exactly* what is on screen without saving to DB, CommitToModel is okay provided we don't save to DB.
            // But CommitToModel updates the _model by reference. 
            // Let's create a temporary clone or just commit to the current model (which is the one being edited).
            
            Wrapper.CommitToModel();
            
            try 
            {
                BusyText = "Generating profile...";
                IsBusy = true;
                var path = await _exportService.GenerateEmployeeProfileHtmlAsync(Wrapper.Model);
                await _exportService.OpenFileAsync(path);
            }
            catch (Exception ex)
            {
                if (_dialogService != null) 
                    await _dialogService.ShowAlertAsync("Error", $"Failed to print profile: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Methods

        public virtual async Task Load(Guid employeeId)
        {
            try 
            {
                IsBusy = true;
                _existingStaffId = employeeId;
                Title = "Edit Employee";
                SaveButtonText = "Save Changes";

                // Ensure users are loaded BEFORE setting the wrapper to guarantee binding works
                await InitializeAsync();

                // Small delay to ensure UI binding context updates (sometimes needed for ComboBox items source propagation)
                await Task.Delay(50);

                var employeeDto = await _employeeService.GetEmployeeAsync(employeeId);
                if (employeeDto == null)
                {
                     await _dialogService.ShowAlertAsync("Error", "Employee not found.");
                     CloseRequested?.Invoke(this, EventArgs.Empty);
                     return;
                }

                // Map DTO to Entity for Wrapper
                // We use the entity because the wrapper is built around it (Validation, etc)
                // And simpler than creating a DTO wrapper right now.
                var entity = ToEntity(employeeDto);

                Wrapper = new EmployeeWrapper(entity);
                Wrapper.PropertyChanged += (s, e) => 
                {
                    if (e.PropertyName == nameof(EmployeeWrapper.FirstName) || e.PropertyName == nameof(EmployeeWrapper.LastName))
                        OnPropertyChanged(nameof(DisplayName));
                    
                    SaveCommand.NotifyCanExecuteChanged();
                };

                if (Wrapper.LinkedUserId.HasValue)
                {
                    _ = Task.Run(async () => 
                    {
                        var user = await _userRepository.GetByIdAsync(Wrapper.LinkedUserId.Value);
                        if (user != null)
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentPermissions = user.Permissions);
                        }
                    });
                }
                
                UpdatePermissionsButtonVisibility();

                // Sanitize Leave Dates
                if (Wrapper.LeaveCycleStartDate.HasValue && Wrapper.LeaveCycleStartDate.Value < new DateTime(1900, 1, 1))
                {
                     Wrapper.LeaveCycleStartDate = null;
                }

                _ = RefreshBalanceAsync();
                
                UpdateAccrualRule();
                
                
                // Banking Logic
                LoadBankInfo(entity.BankName);

                OnPropertyChanged(nameof(IsOtherBankSelected));
                
                UpdateSystemAccessVisibility();

                OnPropertyChanged(nameof(IsRsaId));
                OnPropertyChanged(nameof(IsPassport));
                OnPropertyChanged(nameof(IsHourly));
                OnPropertyChanged(nameof(IsSalary));
                OnPropertyChanged(nameof(IsPermanent));
                OnPropertyChanged(nameof(IsContract));
                OnPropertyChanged(nameof(IsContractVisible));
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(IsOtherBankSelected));
                
                IsBusy = false;
            }
            catch (Exception)
            {
                IsBusy = false;
                throw;
            }
        }

        private void LoadBankInfo(string? dbBankName)
        {
            var matched = false;
            if (!string.IsNullOrEmpty(dbBankName))
            {
                foreach (var bank in AvailableBanks)
                {
                    if (bank == BankName.None || bank == BankName.Other) continue;
                    if (GetEnumDescription(bank).Equals(dbBankName, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedBank = bank;
                        matched = true;
                        break;
                    }
                }
            }

            if (!matched && !string.IsNullOrEmpty(dbBankName))
            {
                SelectedBank = BankName.Other;
                CustomBankName = dbBankName;
            }
            else if (!matched)
            {
                SelectedBank = BankName.None;
                CustomBankName = string.Empty;
            }
        }

        private string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field!, typeof(DescriptionAttribute));
            return attribute?.Description ?? value.ToString();
        }

        partial void OnWrapperChanged(EmployeeWrapper value)
        {
            if (value != null)
            {
                value.PropertyChanged += (s, e) => 
                {
                    if (e.PropertyName == nameof(EmployeeWrapper.IdNumber))
                    {
                         if (Wrapper.IdType == IdType.RSAId && Wrapper.IdNumber.Length >= 6)
                            CalculateDoBFromRsaId(Wrapper.IdNumber);
                    }
                    else if (e.PropertyName == nameof(EmployeeWrapper.IdType))
                    {
                        if (Wrapper.IdType == IdType.RSAId && Wrapper.IdNumber.Length >= 6)
                            CalculateDoBFromRsaId(Wrapper.IdNumber);
                    }
                    else if (e.PropertyName == nameof(EmployeeWrapper.Branch))
                    {
                        UpdateShiftTimes();
                    }
                    else if (e.PropertyName == nameof(EmployeeWrapper.Role))
                    {
                         UpdatePermissionsButtonVisibility();
                         UpdateSystemAccessVisibility();
                    }
                    else if (e.PropertyName == nameof(EmployeeWrapper.EmploymentType))
                    {
                        UpdateAccrualRule();
                    }
                    else if (e.PropertyName == nameof(EmployeeWrapper.LeaveCycleStartDate))
                    {
                        UpdateSickLeaveCycleEnd();
                    }
                    else if (e.PropertyName == nameof(EmployeeWrapper.FirstName) || e.PropertyName == nameof(EmployeeWrapper.LastName))
                    {
                         OnPropertyChanged(nameof(DisplayName));
                    }
                    else if (e.PropertyName == nameof(EmployeeWrapper.LinkedUserId))
                    {
                         OnPropertyChanged(nameof(HasLinkedUser));
                         HandleLinkedUserChange(Wrapper.LinkedUserId);
                         UpdatePermissionsButtonVisibility();
                    }
                    else if (e.PropertyName == nameof(EmployeeWrapper.AnnualLeaveBalance) || e.PropertyName == nameof(EmployeeWrapper.EmploymentDate))
                    {
                         _ = RefreshBalanceAsync();
                    }
                    
                    SaveCommand.NotifyCanExecuteChanged();
                };
            }
        }

        private void HandleLinkedUserChange(Guid? userId)
        {
            if (userId.HasValue)
            {
                var selectedUser = AvailableUsers.FirstOrDefault(u => u.Id == userId.Value);
                if (selectedUser != null)
                {
                    // Update wrapper with user info if it's currently empty or we want to sync
                    if (string.IsNullOrWhiteSpace(Wrapper.FirstName)) Wrapper.FirstName = selectedUser.FirstName;
                    if (string.IsNullOrWhiteSpace(Wrapper.LastName)) Wrapper.LastName = selectedUser.LastName;
                    if (string.IsNullOrWhiteSpace(Wrapper.Email)) Wrapper.Email = selectedUser.Email;
                    if (string.IsNullOrWhiteSpace(Wrapper.Phone)) Wrapper.Phone = selectedUser.Phone ?? string.Empty;
                    
                    CurrentPermissions = selectedUser.Permissions;
                }
            }
        }

        private void UpdateAccrualRule()
        {
            if (Wrapper.EmploymentType == EmploymentType.Contract)
            {
                LeaveAccrualRule = "Accural: 1 day / 17 days (Annual) | 1 day / 26 days (Sick)";
                if (!_existingStaffId.HasValue && Wrapper.AnnualLeaveBalance == 0) 
                {
                    Wrapper.AnnualLeaveBalance = 0;
                    Wrapper.SickLeaveBalance = 0;
                }
            }
            else
            {
                LeaveAccrualRule = "Standard: 15 Working Days Annual / 30 Days Sick Leave Cycle";
                if (!_existingStaffId.HasValue && Wrapper.AnnualLeaveBalance == 0)
                {
                    Wrapper.SickLeaveBalance = 30;
                }
            }
            _ = RefreshBalanceAsync();
        }

        private void UpdateSickLeaveCycleEnd()
        {
            if (Wrapper.LeaveCycleStartDate.HasValue && Wrapper.LeaveCycleStartDate.Value > new DateTime(1900, 1, 1))
            {
                var endDate = Wrapper.LeaveCycleStartDate.Value.AddMonths(36).AddDays(-1);
                SickLeaveCycleEndDisplay = endDate.ToString("dd MMM yyyy");
            }
            else
            {
                SickLeaveCycleEndDisplay = "N/A";
            }
            _ = RefreshBalanceAsync();
        }

        private void UpdateShiftTimes()
        {
            var jhbStart = new TimeSpan(7, 0, 0);
            var jhbEnd = new TimeSpan(16, 45, 0);
            var cptStart = new TimeSpan(7, 0, 0);
            var cptEnd = new TimeSpan(16, 30, 0);

            if (string.Equals(Wrapper.Branch, "Johannesburg", StringComparison.OrdinalIgnoreCase))
            {
                Wrapper.ShiftStartTime = jhbStart;
                Wrapper.ShiftEndTime = jhbEnd;
            }
            else if (string.Equals(Wrapper.Branch, "Cape Town", StringComparison.OrdinalIgnoreCase))
            {
                Wrapper.ShiftStartTime = cptStart;
                Wrapper.ShiftEndTime = cptEnd;
            }
        }

        [RelayCommand]
        private void ClosePermissions()
        {
            IsPermissionsPopupVisible = false;
        }

        [RelayCommand]
        private async Task OpenPermissions()
        {
            var currentUser = _authService.CurrentUser;
            if (currentUser?.UserRole != UserRole.Admin)
            {
                await _dialogService.ShowAlertAsync("Access Denied", "Only administrators can manage user access and permissions.");
                return;
            }

            var linkedUser = AvailableUsers.FirstOrDefault(u => u.Id == Wrapper.LinkedUserId);
            var role = linkedUser?.UserRole ?? UserRole.Guest;

            var vm = new EmployeePermissionsViewModel(DisplayName, CurrentPermissions, role);
            vm.OnSaved = (p) => 
            {
                CurrentPermissions = p;
                IsPermissionsPopupVisible = false;
            };

            PermissionsPopupVM = vm;
            IsPermissionsPopupVisible = true;
        }

        private void UpdatePermissionsButtonVisibility()
        {
            var currentUser = _authService.CurrentUser;
            if (currentUser?.UserRole != UserRole.Admin)
            {
                ShowPermissionsButton = false;
                return;
            }

            // Only show for the Office employee role. 
            // SiteManagers are granted full access via direct role checks and don't need toggles.
            bool isRoleManaged = Wrapper.Role == EmployeeRole.Office;
            
            // Check if the linked user (if any) is an Admin. If so, hide permissions button.
            bool isLinkedAdmin = false;
            if (Wrapper.LinkedUserId.HasValue)
            {
                var linkedUser = AvailableUsers.FirstOrDefault(u => u.Id == Wrapper.LinkedUserId.Value);
                // Treat SiteManager as Admin for button visibility too, as they have full access for now
                isLinkedAdmin = linkedUser?.UserRole == UserRole.Admin || linkedUser?.UserRole == UserRole.SiteManager;
            }

            ShowPermissionsButton = isRoleManaged && !isLinkedAdmin;
            ShowPermissionsButton = isRoleManaged && !isLinkedAdmin;
        }

        private void UpdateSystemAccessVisibility()
        {
            IsSystemAccessVisible = Wrapper.Role == EmployeeRole.Office || Wrapper.Role == EmployeeRole.SiteManager;
        }

        private void CalculateDoBFromRsaId(string id)
        {
            if (id.Length < 6) return;
            string datePart = id.Substring(0, 6);
            
            if (DateTime.TryParseExact(datePart, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dob))
            {
                _calculatedDoB = dob;
                Wrapper.DoB = dob;
            }
        }

        private async Task RefreshBalanceAsync()
        {
            if (_leaveService == null || IsBusy) return;

            try
            {
                // Create a temporary employee object to calculate balance without saving to DB
                var tempEmployee = new Employee
                {
                    Id = _existingStaffId ?? Guid.Empty,
                    AnnualLeaveBalance = Wrapper.AnnualLeaveBalance,
                    EmploymentDate = Wrapper.EmploymentDate
                };

                var balance = await _leaveService.CalculateCurrentLeaveBalanceAsync(tempEmployee);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentAnnualLeaveBalance = balance);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmployeeDetailViewModel] Error refreshing balance: {ex.Message}");
            }
        }

        #endregion
        
        private Employee ToEntity(EmployeeDto dto)
        {
            return new Employee
            {
                Id = dto.Id,
                LinkedUserId = dto.LinkedUserId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                IdNumber = dto.IdNumber,
                IdType = dto.IdType,
                PermitNumber = dto.PermitNumber,
                Email = dto.Email,
                Phone = dto.Phone,
                PhysicalAddress = dto.PhysicalAddress,
                DoB = dto.DoB,
                EmployeeNumber = dto.EmployeeNumber,
                Role = dto.Role,
                Status = dto.Status,
                EmploymentType = dto.EmploymentType,
                ContractDuration = dto.ContractDuration,
                EmploymentDate = dto.EmploymentDate,
                Branch = dto.Branch,
                LivesInCompanyHousing = dto.LivesInCompanyHousing,
                ShiftStartTime = dto.ShiftStartTime,
                ShiftEndTime = dto.ShiftEndTime,
                RateType = dto.RateType,
                HourlyRate = dto.HourlyRate,
                TaxNumber = dto.TaxNumber,
                BankName = dto.BankName,
                AccountNumber = dto.AccountNumber,
                BranchCode = dto.BranchCode,
                AccountType = dto.AccountType,
                AnnualLeaveBalance = dto.AnnualLeaveBalance,
                SickLeaveBalance = dto.SickLeaveBalance,
                LeaveBalance = dto.LeaveBalance,
                LeaveCycleStartDate = dto.LeaveCycleStartDate,
                NextOfKinName = dto.NextOfKinName,
                NextOfKinRelation = dto.NextOfKinRelation,
                NextOfKinPhone = dto.NextOfKinPhone,
                EmergencyContactName = dto.EmergencyContactName,
                EmergencyContactPhone = dto.EmergencyContactPhone,
                RowVersion = dto.RowVersion ?? new byte[0]
            };
        }
    }
}
