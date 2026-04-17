using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents photographic evidence attached to an <see cref="Incident"/> report.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>IncidentPhotos</c> table (or related storage).
    /// <b>How:</b> Linked to a parent <see cref="Incident"/>. Stores the image data (or reference) and a caption.
    /// </remarks>
    public class IncidentPhoto : BaseEntity
    {


        /// <summary> Foreign Key linking to the parent <see cref="Incident"/>. </summary>
        public Guid IncidentId { get; set; }

        /// <summary> The original file name of the photo. </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary> The relative path to the stored file. </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary> The size of the file (e.g., "1.2 MB"). </summary>
        public string FileSize { get; set; } = string.Empty;

        /// <summary> A description or caption for the photo (e.g., "Damage to left fender"). </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary> Name of the user who uploaded the photo. </summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary> Timestamp when the photo was uploaded/captured. </summary>
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;


    }
}
