using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents the association of an <see cref="Employee"/> to a <see cref="Project"/>.
    /// Used for tracking which employees are assigned to the project team.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>ProjectTeamMembers</c> join table.
    /// <b>How:</b> Many-to-Many relationship facilitator.
    /// </remarks>
    public class ProjectTeamMember : BaseEntity
    {
        /// <summary> Foreign Key linking to the <see cref="Project"/>. </summary>
        public Guid ProjectId { get; set; }

        /// <summary> Foreign Key linking to the <see cref="Employee"/>. </summary>
        public Guid EmployeeId { get; set; }

        /// <summary> Timestamp when the employee was added to the project team. </summary>
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Project? Project { get; set; }
        public virtual Employee? Employee { get; set; }
    }
}
