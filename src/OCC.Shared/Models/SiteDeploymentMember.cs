using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a single employee allocated to a <see cref="SiteDeployment"/>.
    /// The Site Manager can mark an employee as absent at receipt time (e.g., no-show despite being auto-clocked in).
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>SiteDeploymentMembers</c> table.
    /// <b>How:</b> When the deployment is received, all members where <see cref="IsAbsent"/> is false
    /// have their <see cref="AttendanceRecord.ProjectId"/> set to the deployment's project,
    /// attributing their hours to that project for HSEQ safe-hour calculations.
    /// </remarks>
    public class SiteDeploymentMember : BaseEntity
    {
        /// <summary> Foreign key to the parent <see cref="SiteDeployment"/>. </summary>
        public Guid SiteDeploymentId { get; set; }

        /// <summary> Navigation property to the parent deployment. </summary>
        public virtual SiteDeployment? SiteDeployment { get; set; }

        /// <summary> Foreign key to the <see cref="Employee"/> allocated in this deployment. </summary>
        public Guid EmployeeId { get; set; }

        /// <summary> Navigation property to the allocated Employee. </summary>
        public virtual Employee? Employee { get; set; }

        /// <summary>
        /// Set to true by the Site Manager at receipt if the employee did not arrive on site.
        /// Absent members will NOT have their AttendanceRecord.ProjectId updated.
        /// </summary>
        public bool IsAbsent { get; set; } = false;
    }
}
