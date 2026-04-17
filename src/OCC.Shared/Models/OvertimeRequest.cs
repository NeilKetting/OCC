using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a request for authorization to work overtime.
    /// Used for payroll calculations and resource planning.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>OvertimeRequests</c> table.
    /// <b>How:</b> Linked to an <see cref="Employee"/>. If approved, this may feed into the <see cref="WageRun"/> 
    /// calculations depending on the business rules. Status is tracked using <see cref="LeaveStatus"/> (Pending, Approved, Rejected).
    /// </remarks>
    public class OvertimeRequest : BaseEntity
    {


        /// <summary> Foreign key to the <see cref="Employee"/> requesting overtime. </summary>
        [Required]
        public Guid EmployeeId { get; set; }

        /// <summary> Navigation property to the employee entity. </summary>
        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        /// <summary> The calendar date for which overtime is requested. </summary>
        public DateTime Date { get; set; }

        /// <summary> The proposed start time of the overtime shift. </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary> The proposed end time of the overtime shift. </summary>
        public TimeSpan EndTime { get; set; }

        /// <summary> Justification for the overtime (e.g., "Project Deadline"). </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary> Explanation if the request is denied. </summary>
        public string? RejectionReason { get; set; }

        /// <summary> Current approval status (Pending, Approved, Rejected). </summary>
        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        /// <summary> ID of the manager who authorized or rejected the request. </summary>
        public Guid? ApproverId { get; set; }

        /// <summary> Date when the request was approved/rejected. </summary>
        public DateTime? ActionedDate { get; set; }

        /// <summary> Additional notes from the administrator. </summary>
        public string? AdminComment { get; set; }

        /// <summary> Date the request was submitted. </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Calculated duration of the overtime in hours.
        /// Not persisted in the database.
        /// </summary>
        [NotMapped]
        public double DurationHours => (EndTime - StartTime).TotalHours;
    }
}
