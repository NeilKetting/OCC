using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a record of a user dismissing a specific notification or suggested action.
    /// Used to persistently suppress recurring alerts (e.g., pending approvals or birthday reminders) 
    /// that the user has explicitly chosen to ignore.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>NotificationDismissals</c> table.
    /// <b>How:</b> Queries check this table before generating dynamic notifications to ensure dismissed items are skipped.
    /// </remarks>
    public class NotificationDismissal : BaseEntity
    {


        /// <summary> The ID of the <see cref="User"/> who performed the dismissal. </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// The unique ID of the target entity associated with the notification 
        /// (e.g., UserId for registration requests, RequestId for Leave, or EmployeeId for Birthdays).
        /// </summary>
        public Guid EntityId { get; set; }

        /// <summary>
        /// A string identifier for the category of notification (e.g., "UserRegistration", "LeaveRequest", "Birthday").
        /// </summary>
        public string NotificationType { get; set; } = string.Empty;

        /// <summary> The timestamp when the dismissal action occurred. </summary>
        public DateTime DismissedAt { get; set; } = DateTime.UtcNow;
    }
}
