using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.DTOs;
using OCC.Shared.Models;

namespace OCC.WpfClient.Features.EmployeeHub.Models
{
    public partial class EmployeeModel : ObservableValidator
    {
        public Guid Id { get; set; }

        [ObservableProperty]
        [Required(ErrorMessage = "First name is required")]
        private string _firstName = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Last name is required")]
        private string _lastName = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Employee number is required")]
        private string _employeeNumber = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "ID Number is required")]
        private string _idNumber = string.Empty;

        [ObservableProperty]
        private IdType _idType = IdType.RSAId;

        [ObservableProperty]
        private string? _permitNumber;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _physicalAddress = string.Empty;

        [ObservableProperty]
        private EmployeeRole _role = EmployeeRole.GeneralWorker;

        [ObservableProperty]
        private EmployeeStatus _status = EmployeeStatus.Active;

        [ObservableProperty]
        private EmploymentType _employmentType = EmploymentType.Permanent;

        [ObservableProperty]
        private string? _contractDuration;

        [ObservableProperty]
        private string _branch = "Johannesburg";

        [ObservableProperty]
        private double _hourlyRate;

        [ObservableProperty]
        private RateType _rateType = RateType.Hourly;

        [ObservableProperty]
        private DateTime _employmentDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _doB = new DateTime(1990, 1, 1);

        [ObservableProperty]
        private TimeSpan? _shiftStartTime = new TimeSpan(7, 0, 0);

        [ObservableProperty]
        private TimeSpan? _shiftEndTime = new TimeSpan(16, 45, 0);

        // Banking Details
        [ObservableProperty] private BankName _bankName = BankName.None;
        [ObservableProperty] private string? _accountNumber;
        [ObservableProperty] private string? _branchCode;
        [ObservableProperty] private string? _accountType;

        // Leave Balances
        [ObservableProperty] private double _annualLeaveBalance;
        [ObservableProperty] private double _sickLeaveBalance = 30;
        [ObservableProperty] private DateTime? _leaveCycleStartDate;

        // Identity Link
        [ObservableProperty] private Guid? _linkedUserId;

        // Metadata
        [ObservableProperty] private string _taxNumber = string.Empty;
        [ObservableProperty] private bool _livesInCompanyHousing;

        // Next of Kin / Emergency
        [ObservableProperty] private string? _nextOfKinName;
        [ObservableProperty] private string? _nextOfKinRelation;
        [ObservableProperty] private string? _nextOfKinPhone;
        [ObservableProperty] private string? _emergencyContactName;
        [ObservableProperty] private string? _emergencyContactPhone;
        [ObservableProperty] private byte[]? _rowVersion;

        public string DisplayName => $"{FirstName} {LastName}";

        public EmployeeModel()
        {
        }

        public EmployeeModel(EmployeeDto dto)
        {
            Id = dto.Id;
            UpdateFromEntity(dto);
        }

        public void UpdateFromEntity(EmployeeDto dto)
        {
            LinkedUserId = dto.LinkedUserId;
            FirstName = dto.FirstName;
            LastName = dto.LastName;
            EmployeeNumber = dto.EmployeeNumber;
            IdNumber = dto.IdNumber;
            IdType = dto.IdType;
            PermitNumber = dto.PermitNumber;
            Email = dto.Email;
            Phone = dto.Phone;
            PhysicalAddress = dto.PhysicalAddress;
            Role = dto.Role;
            Status = dto.Status;
            EmploymentType = dto.EmploymentType;
            ContractDuration = dto.ContractDuration;
            Branch = dto.Branch;
            HourlyRate = dto.HourlyRate;
            RateType = dto.RateType;
            EmploymentDate = dto.EmploymentDate;
            DoB = dto.DoB;
            ShiftStartTime = dto.ShiftStartTime;
            ShiftEndTime = dto.ShiftEndTime;
            // Map string BankName from DTO to Enum
            if (!string.IsNullOrEmpty(dto.BankName))
            {
                foreach (BankName bank in Enum.GetValues(typeof(BankName)))
                {
                    if (bank.ToString().Equals(dto.BankName, StringComparison.OrdinalIgnoreCase))
                    {
                        BankName = bank;
                        break;
                    }
                }
            }
            AccountNumber = dto.AccountNumber;
            BranchCode = dto.BranchCode;
            AccountType = dto.AccountType;
            AnnualLeaveBalance = dto.AnnualLeaveBalance;
            SickLeaveBalance = dto.SickLeaveBalance;
            LeaveCycleStartDate = dto.LeaveCycleStartDate;
            TaxNumber = dto.TaxNumber;
            LivesInCompanyHousing = dto.LivesInCompanyHousing;
            NextOfKinName = dto.NextOfKinName;
            NextOfKinRelation = dto.NextOfKinRelation;
            NextOfKinPhone = dto.NextOfKinPhone;
            EmergencyContactName = dto.EmergencyContactName;
            EmergencyContactPhone = dto.EmergencyContactPhone;
            RowVersion = dto.RowVersion;
        }

        public void Validate() => ValidateAllProperties();

        public Employee ToEntity()
        {
            return new Employee
            {
                Id = Id,
                LinkedUserId = LinkedUserId,
                FirstName = FirstName,
                LastName = LastName,
                EmployeeNumber = EmployeeNumber,
                IdNumber = IdNumber,
                IdType = IdType,
                PermitNumber = PermitNumber,
                Email = Email,
                Phone = Phone,
                PhysicalAddress = PhysicalAddress,
                Role = Role,
                Status = Status,
                EmploymentType = EmploymentType,
                ContractDuration = ContractDuration,
                Branch = Branch,
                HourlyRate = HourlyRate,
                RateType = RateType,
                EmploymentDate = EmploymentDate,
                DoB = DoB,
                ShiftStartTime = ShiftStartTime,
                ShiftEndTime = ShiftEndTime,
                BankName = BankName != BankName.None ? BankName.ToString() : (string?)null,
                AccountNumber = AccountNumber,
                BranchCode = BranchCode,
                AccountType = AccountType,
                AnnualLeaveBalance = AnnualLeaveBalance,
                SickLeaveBalance = SickLeaveBalance,
                LeaveCycleStartDate = LeaveCycleStartDate,
                TaxNumber = TaxNumber,
                LivesInCompanyHousing = LivesInCompanyHousing,
                NextOfKinName = NextOfKinName,
                NextOfKinRelation = NextOfKinRelation,
                NextOfKinPhone = NextOfKinPhone,
                EmergencyContactName = EmergencyContactName,
                EmergencyContactPhone = EmergencyContactPhone,
                RowVersion = RowVersion
            };
        }
    }
}
