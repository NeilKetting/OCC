using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a specific block of time dedicated to a project task.
    /// Unlike <see cref="AttendanceRecord"/> which tracks presence, this tracks productivity/allocation.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>TimeRecords</c> table.
    /// <b>How:</b> Linked to a <see cref="Project"/> and <see cref="ProjectTask"/>. 
    /// Used for costing projects and analyzing efficiency.
    /// </remarks>
    public class TimeRecord : BaseEntity
    {

        
        /// <summary> Optional user ID if the record is linked to a system user. </summary>
        public Guid? UserId { get; set; }

        /// <summary> Foreign Key linking to the <see cref="Employee"/> performing the work. </summary>
        public Guid? EmployeeId { get; set; }
        
        /// <summary> Foreign Key linking to the <see cref="Project"/>. </summary>
        public Guid ProjectId { get; set; }

        /// <summary> Foreign Key linking to the specific <see cref="ProjectTask"/>. </summary>
        public Guid TaskId { get; set; }
        
        /// <summary> The date the work was performed. </summary>
        public DateTime Date { get; set; }

        /// <summary> Duration of work in hours. </summary>
        public double Hours { get; set; }
        
        /// <summary> Specific description of what was done during this time. </summary>
        public string? Comment { get; set; }        
    }
}
