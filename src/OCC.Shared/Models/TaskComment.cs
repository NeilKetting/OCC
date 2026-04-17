using System;
using System.Linq;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a message or note added to a <see cref="ProjectTask"/>.
    /// Facilitates collaboration and communication on specific tasks.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>TaskComments</c> table.
    /// <b>How:</b> Linked to a <see cref="ProjectTask"/>. Stores author details for display and history.
    /// </remarks>
    public class TaskComment : BaseEntity
    {


        /// <summary> Foreign Key linking to the <see cref="ProjectTask"/>. </summary>
        public Guid TaskId { get; set; }

        /// <summary> Navigation property for the parent task. </summary>
        public virtual ProjectTask? ProjectTask { get; set; }

        /// <summary> Name of the user who posted the comment. </summary>
        public string AuthorName { get; set; } = string.Empty;

        /// <summary> Email of the user who posted the comment. </summary>
        public string AuthorEmail { get; set; } = string.Empty;

        /// <summary> The actual text content of the message. </summary>
        public string Content { get; set; } = string.Empty;

        // CreatedAt provided by BaseEntity (CreatedAtUtc)

        
        /// <summary>
        /// Generated initials of the author for UI avatars (e.g., "John Doe" -> "JD").
        /// </summary>
        public string Initials => !string.IsNullOrEmpty(AuthorName) ? 
            string.Join("", AuthorName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(n => n[0])).ToUpper() : "??";
    }
}
