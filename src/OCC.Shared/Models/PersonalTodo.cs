using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a quick personal to-do item for an individual user.
    /// Completely separated from standard project/work tasks.
    /// </summary>
    public class PersonalTodo : BaseEntity
    {
        /// <summary> The owner user of this personal to-do. </summary>
        public Guid UserId { get; set; }
        
        /// <summary> The name or title of the to-do item. </summary>
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        /// <summary> Optional detailed notes or description. </summary>
        public string? Notes { get; set; }
        
        /// <summary> Optional target completion date. </summary>
        public DateTime? DueDate { get; set; }
        
        /// <summary> Completion status indicator. </summary>
        public bool IsComplete { get; set; }
        
        /// <summary> Timestamp when this to-do was marked completed (UTC). </summary>
        public DateTime? CompletedAtUtc { get; set; }
        
        /// <summary> Tracks the EntryID of the corresponding Microsoft Outlook Appointment (if synced). </summary>
        [MaxLength(250)]
        public string? OutlookEventId { get; set; }
    }
}
