using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents the association of an <see cref="Employee"/> to a <see cref="Team"/>.
    /// Used for organizing workforce groups.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>TeamMembers</c> join table.
    /// <b>How:</b> Many-to-Many relationship facilitator. An employee can belong to multiple teams if business rules allow.
    /// </remarks>
    public class TeamMember : BaseEntity
    {


        /// <summary> Foreign Key linking to the <see cref="Team"/>. </summary>
        public Guid TeamId { get; set; }

        /// <summary> Foreign Key linking to the <see cref="Employee"/>. </summary>
        public Guid EmployeeId { get; set; }

        /// <summary> Timestamp when the employee was added to the team. </summary>
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        // Navigation (optional depending on EF setup, but good for shared)
        // public Team? Team { get; set; }
        // public Employee? Employee { get; set; } 
    }
}
