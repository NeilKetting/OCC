using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a user-submitted issue or error report within the application.
    /// Used by developers and admins to track and resolve software defects.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>BugReports</c> table.
    /// <b>How:</b> Often captured via the "Report Bug" feature in the UI, which may auto-populate context like <see cref="ViewName"/>.
    /// </remarks>
    public enum BugReportType
    {
        Bug,
        Suggestion,
        Question,
        Other
    }

    public class BugReport : BaseEntity
    {
        /// <summary> The category of the report. </summary>
        public BugReportType Type { get; set; } = BugReportType.Bug;


        /// <summary> Optional ID of the user who submitted the report (if logged in). </summary>
        public Guid? ReporterId { get; set; }

        /// <summary> Display name of the reporter. </summary>
        [Required]
        public string ReporterName { get; set; } = string.Empty;

        /// <summary> Timestamp when the report was created. </summary>
        public DateTime ReportedDate { get; set; } = DateTime.UtcNow;

        /// <summary> The name of the screen or component where the issue occurred. </summary>
        [Required]
        public string ViewName { get; set; } = string.Empty;

        /// <summary> Detailed explanation of the problem or steps to reproduce. </summary>
        [Required]
        public string Description { get; set; } = string.Empty;

        /// <summary> Current status of the bug (e.g., "Open", "Fixed", "Won't Fix"). </summary>
        public string Status { get; set; } = "Open";

        /// <summary> Internal notes or feedback from the development team. </summary>
        public string? AdminComments { get; set; }

        /// <summary> Base64 encoded screenshot of the application state at the time of the report. </summary>
        public string? ScreenshotBase64 { get; set; }

        /// <summary> Discussion thread related to this bug. </summary>
        public virtual ICollection<BugComment> Comments { get; set; } = new List<BugComment>();
    }
}
