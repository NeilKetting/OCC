using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents an individual employee within the OCC system.
    /// This model is the central point for managing human resources, payroll data, and identity linking.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> This entity is persisted in the <c>Employees</c> table in the database and is used across the API and Client applications.
    /// <b>How:</b> It can be linked to a system login via <see cref="LinkedUserId"/>, allowing employees to access the mobile or desktop apps.
    /// It also tracks leave balances, accrual start dates, and shift patterns used by the attendance and wage run systems.
    /// </remarks>
    public class Employee : BaseEntity
    {


        /// <summary>
        /// Optional link to a User Login (Identity).
        /// Allows linking this Resource (Employee) to a System User (Login).
        /// </summary>
        public Guid? LinkedUserId { get; set; }

        /// <summary>
        /// Determines if the rate is Hourly or a Monthly Salary.
        /// </summary>
        public RateType RateType { get; set; } = RateType.Hourly; // Hourly or Monthly
        /// <summary>
        /// Hourly pay rate in local currency.
        /// </summary>
        public double HourlyRate { get; set; } // Current Rate
        /// <summary>
        /// The employee's South African Revenue Service (SARS) tax registration number.
        /// </summary>
        public string TaxNumber { get; set; } = string.Empty;
        
        /// <summary>
        /// Current accrued annual leave days.
        /// Usually accrues at a rate of 1 day per 17 days worked or as per BCEA guidelines.
        /// </summary>
        public double AnnualLeaveBalance { get; set; }

        /// <summary>
        /// Remaining sick leave days for the current 36-month cycle.
        /// Defaults to 30 days per cycle for full-time employees.
        /// </summary>
        public double SickLeaveBalance { get; set; } = 30;

        /// <summary>
        /// The date from which leave cycles are calculated. 
        /// Important for resetting sick leave balances every 3 years.
        /// </summary>
        public DateTime? LeaveCycleStartDate { get; set; }

        /// <summary>
        /// Employee's first name.
        /// </summary>
        [Required]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Employee's last name.
        /// </summary>
        [Required]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Type of identification document used.
        /// </summary>
        public IdType IdType { get; set; } = IdType.RSAId;

        /// <summary>
        /// Employee's identification number (RSA ID or Passport Number).
        /// </summary>
        [Required]
        public string IdNumber { get; set; } = string.Empty;

        /// <summary>
        /// Optional Permit Number for non-RSA residents.
        /// </summary>
        public string? PermitNumber { get; set; }

        /// <summary>
        /// Contact email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Contact phone number.
        /// </summary>
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// Employee's physical residential address.
        /// </summary>
        public string PhysicalAddress { get; set; } = string.Empty;

        /// <summary>
        /// Date of Birth, typically calculated from RSA ID or manually entered.
        /// Defaults to 30 years ago.
        /// </summary>
        public DateTime DoB { get; set; } = new DateTime(1990, 1, 1);

        /// <summary>
        /// The primary skill set of the employee.
        /// </summary>
        public EmployeeRole Role { get; set; } = EmployeeRole.GeneralWorker;



        /// <summary>
        /// Computed full name of the employee.
        /// </summary>
        public string DisplayName => $"{FirstName}, {LastName}".Trim();

        /// <summary>
        /// Assigned employee number (e.g., EMP001).
        /// </summary>
        public string EmployeeNumber { get; set; } = string.Empty;

        /// <summary>
        /// Type of employment (Permanent or Contract).
        /// </summary>
        public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

        /// <summary>
        /// Duration of the contract if EmploymentType is Contract (e.g., "6 Months").
        /// This is nullable as it only applies to contract workers.
        /// </summary>
        public string? ContractDuration { get; set; }

        /// <summary>
        /// The date the employee started working.
        /// </summary>
        public DateTime EmploymentDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Valid values: "Johannesburg", "Cape Town"
        /// </summary>
        public string Branch { get; set; } = "Johannesburg";

        /// <summary>
        /// Start time of the employee's shift.
        /// </summary>
        public TimeSpan? ShiftStartTime { get; set; } = new TimeSpan(7, 0, 0);

        /// <summary>
        /// End time of the employee's shift.
        /// </summary>
        public TimeSpan? ShiftEndTime { get; set; } = new TimeSpan(16, 45, 0);

        // Banking Details
        /// <summary>
        /// Name of the bank where the employee's salary is paid.
        /// </summary>
        public string? BankName { get; set; }

        /// <summary>
        /// The employee's bank account number.
        /// </summary>
        public string? AccountNumber { get; set; }

        /// <summary>
        /// The branch code for the employee's bank.
        /// </summary>
        public string? BranchCode { get; set; }

        /// <summary>
        /// The type of account (e.g., Savings, Cheque).
        /// </summary>
        public string? AccountType { get; set; }

        /// <summary>
        /// Available Annual Leave balance in days.
        /// </summary>
        public double LeaveBalance { get; set; } = 15.0; // Default Logic? 
        

        /// <summary>
        /// Current status of the employee (Active, Inactive, Terminated).
        /// </summary>
        /// <summary>
        /// Current status of the employee (Active, Inactive, Terminated).
        /// </summary>
        public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

        /// <summary>
        /// Indicates if the employee lives in company housing (used for splitting gas bills).
        /// </summary>
        public bool LivesInCompanyHousing { get; set; }

        // Emergency & Next of Kin
        
        /// <summary> Name of the next of kin. </summary>
        public string? NextOfKinName { get; set; }
        /// <summary> Types of relation (e.g. Spouse, Parent, Sibling). </summary>
        public string? NextOfKinRelation { get; set; }
        /// <summary> Contact number for next of kin. </summary>
        public string? NextOfKinPhone { get; set; }

        /// <summary> Name of emergency contact (if different from NOK). </summary>
        public string? EmergencyContactName { get; set; }
        /// <summary> Contact number for emergency contact. </summary>
        public string? EmergencyContactPhone { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public enum EmployeeStatus
    {
        Active,
        Inactive,
        Terminated
    }

    public enum RateType
    {
        Hourly,
        MonthlySalary
    }
    
    // ... rest of enums


    /// <summary>
    /// Defines the nature of the employment contract.
    /// </summary>
    public enum EmploymentType
    {
        Permanent,
        Contract
    }

    /// <summary>
    /// Defines the role or trade of the employee.
    ///Ids are explicitly assigned to prevent shifting if items are reordered.
    /// </summary>
    public enum EmployeeRole
    {
        // Legacy / Existing Roles
        Office = 0,
        GeneralWorker = 1,
        Builder = 2,
        Tiler = 3,
        Painter = 4,
        Electrician = 5,
        Plumber = 6,
        Supervisor = 7,             // Generic Supervisor (legacy)
        ExternalContractor = 8,

        // New Client Requested Roles
        BuildingSupervisor = 9,
        PlasterSupervisor = 10,
        ShopfittingSupervisor = 11,
        PaintingSupervisor = 12,
        LabourSupervisor = 13,
        Cleaner = 14,
        Shopfitter = 15,
        Plasterer = 16,
        PlasterLabour = 17,
        LegacySeniorForeman = 18,
        SnrForeman = 19,
        Welder = 20,
        SiteManager = 21,
        BrickLayer = 22,
        Driver = 23,
        JnrForeman = 24
    }

    /// <summary>
    /// Supported identification document types.
    /// </summary>
    public enum IdType
    {
        RSAId,
        Passport
    }
}
