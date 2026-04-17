using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a system-generated alert or message for a user.
    /// Used to inform users about important events, deadlines, or actions required.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>Notifications</c> table.
    /// <b>How:</b> Can be targeted to a specific <see cref="UserId"/>. 
    /// Includes optional <see cref="TargetAction"/> to deep-link to relevant parts of the app.
    /// </remarks>
    public class Notification : BaseEntity
    {


        /// <summary> The headline or subject of the notification. </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary> The body text content of the notification. </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary> The time at which the notification was generated. </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary> Flag indicating if the user has seen this notification. </summary>
        public bool IsRead { get; set; }

        /// <summary> The category context of the notification (Reminder, Alert, etc.). </summary>
        public NotificationType Type { get; set; } = NotificationType.Reminder;

        /// <summary> An optional route or command string that the UI can execute when clicked. </summary>
        public string? TargetAction { get; set; }

        /// <summary> The ID of the specific user this notification is intended for. </summary>
        public Guid? UserId { get; set; }
    }

    /// <summary>
    /// Classifies the urgency or nature of a notification.
    /// </summary>
    public enum NotificationType
    {
        /// <summary> Routine reminder for scheduled tasks. </summary>
        Reminder,
        /// <summary> Urgent warning or critical information. </summary>
        Alert,
        /// <summary> Information about system or data changes. </summary>
        Update,
        /// <summary> Direct communication or message. </summary>
        Message
    }
}
