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
        public string DisplayName => $"{FirstName}, {LastName}".Trim();
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
        public double LeaveBalance { get; set; }
        public DateTime EmploymentDate { get; set; }
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
}
