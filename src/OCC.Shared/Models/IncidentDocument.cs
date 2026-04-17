using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a report document attached to an <see cref="Incident"/> report.
    /// </summary>
    public class IncidentDocument : BaseEntity
    {
        /// <summary> Foreign Key linking to the parent <see cref="Incident"/>. </summary>
        public Guid IncidentId { get; set; }

        /// <summary> The original file name of the document. </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary> The relative path to the stored file. </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary> The size of the file (e.g., "1.2 MB"). </summary>
        public string FileSize { get; set; } = string.Empty;

        /// <summary> Name of the user who uploaded the document. </summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary> Timestamp when the document was uploaded. </summary>
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
