using System;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a message or update posted on a <see cref="BugReport"/>.
    /// Used for communication between users and developers regarding a specific issue.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>BugComments</c> table.
    /// <b>How:</b> Linked to a <see cref="BugReport"/>. Can be flagged as a developer comment via <see cref="IsDevComment"/>.
    /// </remarks>
    public class BugComment : BaseEntity
    {

        
        /// <summary> Foreign Key linking to the parent <see cref="BugReport"/>. </summary>
        public Guid BugReportId { get; set; }
        
        /// <summary> Name of the person who posted the comment. </summary>
        public string AuthorName { get; set; } = string.Empty;
        
        /// <summary> Email address of the author. </summary>
        public string AuthorEmail { get; set; } = string.Empty;
        
        /// <summary> The text content of the comment. </summary>
        [Required]
        public string Content { get; set; } = string.Empty;
        
        // CreatedAt provided by BaseEntity

        
        /// <summary> If true, indicates the comment was made by a developer or admin (useful for UI styling). </summary>
        public bool IsDevComment { get; set; }

        /// <summary> If true, indicates this comment was marked as the solution by a developer or admin. </summary>
        public bool IsSolution { get; set; }

        /// <summary>
        /// Generated initials of the author for UI avatars (e.g., "Jane Smith" -> "JS").
        /// </summary>
        public string Initials => !string.IsNullOrEmpty(AuthorName) ? 
            string.Join("", AuthorName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(n => n[0])).ToUpper() : "??";
    }
}
