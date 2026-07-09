using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a formal request for time off by an employee.
    /// Handles the workflow from application to approval or rejection.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>LeaveRequests</c> table.
    /// <b>How:</b> Linked to <see cref="Employee"/>. Approved requests should ideally update the 
    /// employee's leave balance in <see cref="Employee.AnnualLeaveBalance"/> or <see cref="Employee.SickLeaveBalance"/>.
    /// </remarks>
    public class LeaveRequest : BaseEntity
    {


        /// <summary> Foreign key to the <see cref="Employee"/> requesting the leave. </summary>
        public Guid EmployeeId { get; set; }

        /// <summary> Navigation property for the requesting employee. </summary>
        public Employee? Employee { get; set; }

        /// <summary> The first day of the leave period. </summary>
        public DateTime StartDate { get; set; }

        /// <summary> The last day of the leave period (inclusive). </summary>
        public DateTime EndDate { get; set; }
        
        /// <summary>
        /// Total number of business days for this leave request (excluding weekends/holidays).
        /// Can be fractional (e.g., 0.5 for half day).
        /// </summary>
        public double NumberOfDays { get; set; }

        /// <summary> The duration category of the leave request (Full Day, Half Day, Hourly). </summary>
        public LeaveDurationType DurationType { get; set; } = LeaveDurationType.FullDay;

        /// <summary> The exact number of hours requested, if DurationType is Hourly. </summary>
        public double? HoursRequested { get; set; }

        /// <summary> The number of paid days allocated for this leave request. </summary>
        public double PaidDays { get; set; }

        /// <summary> The number of unpaid days allocated for this leave request. </summary>
        public double UnpaidDays { get; set; }

        /// <summary> The category of leave being requested (Annual, Sick, Maternity, etc.). </summary>
        public LeaveType LeaveType { get; set; } = LeaveType.Annual;

        /// <summary> The current stage in the approval workflow (Pending, Approved, Rejected). </summary>
        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        /// <summary> The reason provided by the employee for the leave request. </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Feedback or notes from the manager/HR regarding the approval or rejection.
        /// </summary>
        public string? AdminComment { get; set; }
        
        /// <summary>
        /// The unique ID of the supervisor or manager who actioned this request.
        /// </summary>
        public Guid? ApproverId { get; set; }

        /// <summary> The date and time the request was approved or rejected. </summary>
        public DateTime? ActionedDate { get; set; }

        /// <summary> The date the request was originally submitted. </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// If true, this leave will not be paid (e.g., if annual leave balance is zero).
        /// </summary>
        public bool IsUnpaid { get; set; }

        /// <summary> Helper property returning the combined employee display name for report printing. </summary>
        public string EmployeeName => Employee != null ? $"{Employee.FirstName} {Employee.LastName}".Trim() : string.Empty;
    }

    /// <summary>
    /// Defines the legal and policy-based categories of leave available.
    /// </summary>
    public enum LeaveType
    {
        /// <summary> Accrued paid time off. </summary>
        Annual,
        /// <summary> Paid time off for illness or injury. </summary>
        Sick,
        /// <summary> Paid time for family crises ( South African BCEA standard). </summary>
        FamilyResponsibility,
        /// <summary> Approved time for educational purposes. </summary>
        Study,
        /// <summary> Long-term leave for new mothers. </summary>
        Maternity,
        /// <summary> Time off without pay. </summary>
        Unpaid,
        /// <summary> Absent without authorized leave. </summary>
        AbsentWithoutLeave,
        /// <summary> Paid special leave up to 3 days, capped by annual leave balance, thereafter unpaid. </summary>
        CulturalObligations
    }

    /// <summary>
    /// Specifies the duration portion of a leave request.
    /// </summary>
    public enum LeaveDurationType
    {
        /// <summary> Takes the full working day off. </summary>
        FullDay,
        /// <summary> Takes the morning portion off (07:00 to 12:00). </summary>
        MorningHalfDay,
        /// <summary> Takes the afternoon portion off (13:00 to 16:45). </summary>
        AfternoonHalfDay,
        /// <summary> Takes specific hours off (e.g., to visit the bank). </summary>
        Hourly
    }

    /// <summary>
    /// Represents the workflow status of a leave application.
    /// </summary>
    public enum LeaveStatus
    {
        /// <summary> Submitted but not yet reviewed. </summary>
        Pending,
        /// <summary> Authorized by management. </summary>
        Approved,
        /// <summary> Not authorized (requires comment). </summary>
        Rejected,
        /// <summary> Withdrawn by the employee before it was actioned or after approval. </summary>
        Cancelled
    }
}
