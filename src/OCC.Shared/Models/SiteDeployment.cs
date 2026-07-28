using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a daily crew allocation — a named group of employees dispatched to a specific
    /// project site for a given date. Created by the office and "received" by the Site Manager
    /// on the tablet, at which point attendance is attributed to the project.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>SiteDeployments</c> table.
    /// <b>How:</b> The office creates one or more deployments per project per day (e.g., "Crew A - Tilers").
    /// The Site Manager opens OCC.Mobile, confirms the crew is on-site, and marks any absentees.
    /// On confirmation, each present member's <see cref="AttendanceRecord.ProjectId"/> is populated,
    /// enabling project-attributed safe working hours for HSEQ reporting.
    /// </remarks>
    public class SiteDeployment : BaseEntity
    {
        /// <summary> Foreign key to the <see cref="Project"/> this crew is deployed to. </summary>
        public Guid ProjectId { get; set; }

        /// <summary> Navigation property to the associated Project. </summary>
        public virtual Project? Project { get; set; }

        /// <summary> The calendar date this crew is deployed (local date, no time component). </summary>
        public DateTime DeploymentDate { get; set; }

        /// <summary> Alias property for DeploymentDate. </summary>
        [NotMapped]
        public DateTime StartDate
        {
            get => DeploymentDate;
            set => DeploymentDate = value;
        }

        /// <summary> A short descriptive label for the crew (e.g., "Crew A - Tilers", "Afternoon Painters"). </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary> Current lifecycle status of the deployment. </summary>
        public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

        /// <summary>
        /// Optional FK to the <see cref="Employee"/> (Site Manager) who received this crew.
        /// Populated when the SM taps "Confirm Received" on the tablet.
        /// </summary>
        public Guid? ReceivedBySiteManagerId { get; set; }

        /// <summary> Alias property for ReceivedBySiteManagerId. </summary>
        [NotMapped]
        public Guid? SiteManagerId
        {
            get => ReceivedBySiteManagerId;
            set => ReceivedBySiteManagerId = value;
        }

        /// <summary> Navigation property to the receiving Site Manager. </summary>
        public virtual Employee? ReceivedBySiteManager { get; set; }

        /// <summary> UTC timestamp of when the SM confirmed receipt. </summary>
        public DateTime? ReceivedAt { get; set; }

        /// <summary> GPS latitude captured at the moment of crew receipt (soft geo-fence audit trail). </summary>
        public double? ReceivedGpsLatitude { get; set; }

        /// <summary> GPS longitude captured at the moment of crew receipt. </summary>
        public double? ReceivedGpsLongitude { get; set; }

        /// <summary>
        /// Distance in metres from the project's GPS pin at the time of receipt.
        /// Stored for audit; a value above the project's geo-fence threshold triggers a soft warning only.
        /// </summary>
        public double? DistanceFromSiteMetres { get; set; }

        /// <summary> The members (employees) allocated to this deployment. </summary>
        public virtual ICollection<SiteDeploymentMember> Members { get; set; } = new List<SiteDeploymentMember>();
    }

    /// <summary>
    /// Lifecycle states of a <see cref="SiteDeployment"/>.
    /// </summary>
    public enum DeploymentStatus
    {
        /// <summary> Created by office; awaiting Site Manager confirmation. </summary>
        Pending,

        /// <summary> Site Manager has confirmed the crew is on-site. </summary>
        Received,

        /// <summary> Deployment was cancelled before receipt (e.g., crew redirected). </summary>
        Cancelled
    }
}
