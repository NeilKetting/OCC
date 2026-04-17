using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace OCC.Client.ModelWrappers
{
    /// <summary>
    /// A wrapper class for the Employee model that provides validation and MVVM separation.
    /// </summary>
    public partial class EmployeeWrapper : ObservableValidator
    {
        private readonly Employee _model;

        public EmployeeWrapper(Employee model)
        {
            _model = model;
            Initialize();
        }

        public Employee Model => _model;

        public Guid Id => _model.Id;

        [ObservableProperty]
        private string _employeeNumber = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "First name is required")]
        private string _firstName = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Last name is required")]
        private string _lastName = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "ID / Passport number is required")]
        private string _idNumber = string.Empty;

        [ObservableProperty]
        private string? _permitNumber;

        [ObservableProperty]
        private string? _taxNumber;

        [ObservableProperty]
        private IdType _idType = IdType.RSAId;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _physicalAddress = string.Empty;

        [ObservableProperty]
        private EmployeeRole _role = EmployeeRole.GeneralWorker;

        [ObservableProperty]
        private double _hourlyRate;

        [ObservableProperty]
        private EmploymentType _employmentType = EmploymentType.Permanent;

        [ObservableProperty]
        private string? _contractDuration;

        [ObservableProperty]
        private DateTime _employmentDate = DateTime.Now;

        [ObservableProperty]
        private string _branch = "Johannesburg";

        [ObservableProperty]
        private bool _livesInCompanyHousing = false;

        [ObservableProperty]
        private TimeSpan? _shiftStartTime = new TimeSpan(7, 0, 0);

        [ObservableProperty]
        private TimeSpan? _shiftEndTime = new TimeSpan(16, 45, 0);

        [ObservableProperty]
        private string? _bankName;

        [ObservableProperty]
        private string? _accountNumber;

        [ObservableProperty]
        private string? _branchCode;

        [ObservableProperty]
        private string? _accountType;

        [ObservableProperty]
        private double _annualLeaveBalance;

        [ObservableProperty]
        private double _sickLeaveBalance = 30;

        [ObservableProperty]
        private DateTime? _leaveCycleStartDate;

        [ObservableProperty]
        private RateType _rateType = RateType.Hourly;

        [ObservableProperty]
        private Guid? _linkedUserId;

        [ObservableProperty]
        private DateTime _doB = new DateTime(1990, 1, 1);

        // Emergency & Next of Kin
        [ObservableProperty]
        private string? _nextOfKinName;
        [ObservableProperty]
        private string? _nextOfKinRelation;
        [ObservableProperty]
        private string? _nextOfKinPhone;

        [ObservableProperty]
        private string? _emergencyContactName;
        [ObservableProperty]
        private string? _emergencyContactPhone;

        [ObservableProperty]
        private EmployeeStatus _status = EmployeeStatus.Active;

        public void Initialize()
        {
            EmployeeNumber = _model.EmployeeNumber;
            FirstName = _model.FirstName;
            LastName = _model.LastName;
            IdNumber = _model.IdNumber;
            PermitNumber = _model.PermitNumber;
            TaxNumber = _model.TaxNumber;
            IdType = _model.IdType;
            Email = _model.Email;
            Phone = _model.Phone;
            PhysicalAddress = _model.PhysicalAddress;
            Role = _model.Role;
            HourlyRate = _model.HourlyRate;
            EmploymentType = _model.EmploymentType;
            ContractDuration = _model.ContractDuration;
            EmploymentDate = _model.EmploymentDate;
            Branch = _model.Branch;
            LivesInCompanyHousing = _model.LivesInCompanyHousing;
            ShiftStartTime = _model.ShiftStartTime;
            ShiftEndTime = _model.ShiftEndTime;
            BankName = _model.BankName;
            AccountNumber = _model.AccountNumber;
            BranchCode = _model.BranchCode;
            AccountType = _model.AccountType;
            AnnualLeaveBalance = _model.AnnualLeaveBalance;
            SickLeaveBalance = _model.SickLeaveBalance;
            LeaveCycleStartDate = _model.LeaveCycleStartDate;
            RateType = _model.RateType;
            LinkedUserId = _model.LinkedUserId;
            LinkedUserId = _model.LinkedUserId;
            DoB = _model.DoB;

            NextOfKinName = _model.NextOfKinName;
            NextOfKinRelation = _model.NextOfKinRelation;
            NextOfKinPhone = _model.NextOfKinPhone;
            EmergencyContactName = _model.EmergencyContactName;
            EmergencyContactPhone = _model.EmergencyContactPhone;
            Status = _model.Status;

            Validate();
        }

        public void CommitToModel()
        {
            _model.EmployeeNumber = EmployeeNumber;
            _model.FirstName = FirstName;
            _model.LastName = LastName;
            _model.IdNumber = IdNumber;
            _model.PermitNumber = PermitNumber;
            _model.TaxNumber = TaxNumber ?? string.Empty;
            _model.IdType = IdType;
            _model.Email = Email;
            _model.Phone = Phone;
            _model.PhysicalAddress = PhysicalAddress;
            _model.Role = Role;
            _model.HourlyRate = HourlyRate;
            _model.EmploymentType = EmploymentType;
            _model.ContractDuration = ContractDuration;
            _model.EmploymentDate = EmploymentDate;
            _model.Branch = Branch;
            _model.LivesInCompanyHousing = LivesInCompanyHousing;
            _model.ShiftStartTime = ShiftStartTime;
            _model.ShiftEndTime = ShiftEndTime;
            _model.BankName = BankName;
            _model.AccountNumber = AccountNumber;
            _model.BranchCode = BranchCode;
            _model.AccountType = AccountType;
            _model.AnnualLeaveBalance = AnnualLeaveBalance;
            _model.SickLeaveBalance = SickLeaveBalance;
            _model.LeaveCycleStartDate = LeaveCycleStartDate;
            _model.RateType = RateType;
            _model.LinkedUserId = LinkedUserId;
            _model.LinkedUserId = LinkedUserId;
            _model.DoB = DoB;
            
            _model.NextOfKinName = NextOfKinName;
            _model.NextOfKinRelation = NextOfKinRelation;
            _model.NextOfKinPhone = NextOfKinPhone;
            _model.EmergencyContactName = EmergencyContactName;
            _model.EmergencyContactPhone = EmergencyContactPhone;
            _model.Status = Status;
        }

        public void Validate() => ValidateAllProperties();

        public new bool HasErrors => GetErrors().Any();

        partial void OnFirstNameChanged(string value) => ValidateProperty(value, nameof(FirstName));
        partial void OnLastNameChanged(string value) => ValidateProperty(value, nameof(LastName));
        partial void OnIdNumberChanged(string value) => ValidateProperty(value, nameof(IdNumber));
    }
}
