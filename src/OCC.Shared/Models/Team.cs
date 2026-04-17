using System;
using System.Collections.Generic;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a functional work team or department within OCC.
    /// Teams are used to group employees for easier task assignment and management.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>Teams</c> table.
    /// <b>How:</b> A team can have a designated <see cref="LeaderId"/> (linked to an <see cref="Employee"/>) 
    /// and contains a collection of <see cref="Members"/>.
    /// </remarks>
    public class Team : BaseEntity
    {


        /// <summary> The name of the team (e.g., "Tiling Team A", "Johannesburg Painters"). </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary> A brief description of the team's specialization or focus. </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary> Optional foreign key for the employee who leads this team. </summary>
        public Guid? LeaderId { get; set; }

        /// <summary> Navigation property for the individuals assigned to this team. </summary>
        public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
    }
}

