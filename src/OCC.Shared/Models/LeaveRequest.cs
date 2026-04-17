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
        /// </summary>
        public int NumberOfDays { get; set; }

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
        AbsentWithoutLeave
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
