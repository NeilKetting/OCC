using System;
using OCC.Shared.Enums;
using OCC.Shared.Models;

namespace OCC.Shared.DTOs
{
    public class EmployeeSummaryDto
    {
        public Guid Id { get; set; }
        public Guid? LinkedUserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string DisplayName => $"{FirstName} {LastName}".Trim();
        public string FullNameWithNumber => string.IsNullOrEmpty(EmployeeNumber) ? DisplayName : $"{DisplayName} ({EmployeeNumber})";
        public string IdNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public EmployeeRole Role { get; set; }
        public EmployeeStatus Status { get; set; }
        public EmploymentType EmploymentType { get; set; }
        public string Branch { get; set; } = "Johannesburg";
        public RateType RateType { get; set; }
        public double HourlyRate { get; set; }
        public TimeSpan? ShiftStartTime { get; set; }
        public TimeSpan? ShiftEndTime { get; set; }
        public string TaxNumber { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? AccountNumber
        {
            get => BankAccountNumber;
            set => BankAccountNumber = value;
        }
        public double LeaveBalance { get; set; }
        public DateTime EmploymentDate { get; set; }
        public DateTime DoB { get; set; }
        public IdType IdType { get; set; }
        public DateTime? PassportStampDate { get; set; }
        public bool IsPassportStampExpired => IdType == IdType.Passport && (!PassportStampDate.HasValue || (DateTime.Today - PassportStampDate.Value.Date).TotalDays >= 75);
        public bool IsBibc { get; set; }
    }

    public class EmployeeDto
    {
        public Guid Id { get; set; }
        public Guid? LinkedUserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string IdNumber { get; set; } = string.Empty;
        public IdType IdType { get; set; }
        public string? PermitNumber { get; set; }
        public DateTime? PassportStampDate { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PhysicalAddress { get; set; } = string.Empty;
        public DateTime DoB { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public EmployeeRole Role { get; set; }
        public EmployeeStatus Status { get; set; }
        public EmploymentType EmploymentType { get; set; }
        public string? ContractDuration { get; set; }
        public DateTime EmploymentDate { get; set; }
        public string Branch { get; set; } = "Johannesburg";
        public bool LivesInCompanyHousing { get; set; }
        public bool IsBibc { get; set; }
        public TimeSpan? ShiftStartTime { get; set; }
        public TimeSpan? ShiftEndTime { get; set; }
        
        // Payroll/Banking (Partial for security/DPO if needed, but for now full)
        public RateType RateType { get; set; }
        public double HourlyRate { get; set; }
        public string TaxNumber { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? BranchCode { get; set; }
        public string? AccountType { get; set; }
        
        // Balances
        public double AnnualLeaveBalance { get; set; }
        public double SickLeaveBalance { get; set; }
        public double LeaveBalance { get; set; }
        public DateTime? LeaveCycleStartDate { get; set; }

        // Next of Kin
        public string? NextOfKinName { get; set; }
        public string? NextOfKinRelation { get; set; }
        public string? NextOfKinPhone { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class EmployeeReferencesDto
    {
        public int AttendanceCount { get; set; }
        public int TimeRecordCount { get; set; }
        public int TeamMemberCount { get; set; }
        public int ProjectTeamMemberCount { get; set; }
        public int SiteDeploymentMemberCount { get; set; }
        public int LeaveRequestCount { get; set; }
        public int OvertimeRequestCount { get; set; }
        public int EmployeeLoanCount { get; set; }
        public int TaskAssignmentCount { get; set; }
        public int ClockingEventCount { get; set; }
        public int DailyTimesheetCount { get; set; }
        public int HseqTrainingCount { get; set; }
        public int WageRunCount { get; set; }
        public int ProjectManagerCount { get; set; }

        public bool HasReferences =>
            AttendanceCount > 0 ||
            TimeRecordCount > 0 ||
            TeamMemberCount > 0 ||
            ProjectTeamMemberCount > 0 ||
            SiteDeploymentMemberCount > 0 ||
            LeaveRequestCount > 0 ||
            OvertimeRequestCount > 0 ||
            EmployeeLoanCount > 0 ||
            TaskAssignmentCount > 0 ||
            ClockingEventCount > 0 ||
            DailyTimesheetCount > 0 ||
            HseqTrainingCount > 0 ||
            WageRunCount > 0 ||
            ProjectManagerCount > 0;
    }
}
