using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a system-level change log entry.
    /// Tracks who changed what, when, and how (old vs new values).
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>AuditLogs</c> table.
    /// <b>How:</b> Typically generated automatically by <c>SaveChangesAsync</c> interceptors or service logic 
    /// when critical entities are modified.
    /// </remarks>
    public class AuditLog
    {
        /// <summary> Unique primary key (auto-incrementing integer). </summary>
        public int Id { get; set; }

        /// <summary> The ID of the user who performed the action (stored as string for flexibility). </summary>
        public string UserId { get; set; } = string.Empty; 

        /// <summary> The type of operation performed (e.g., "Create", "Update", "Delete"). </summary>
        public string Action { get; set; } = string.Empty; 

        /// <summary> The name of the database table or entity type affected. </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary> The primary key of the specific record that was changed. </summary>
        public string RecordId { get; set; } = string.Empty;

        /// <summary> JSON serialization of the record's state BEFORE the change. </summary>
        public string? OldValues { get; set; }

        /// <summary> JSON serialization of the record's state AFTER the change. </summary>
        public string? NewValues { get; set; } 

        /// <summary> Timestamp of the operation. </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
