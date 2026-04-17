using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a safety-related file or document stored in the system.
    /// Can include policy PDFs, audit templates, or signed safety plans.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>HseqDocuments</c> table (metadata only).
    /// <b>How:</b> <see cref="FilePath"/> points to the actual blob storage location.
    /// </remarks>
    public class HseqDocument : BaseEntity
    {


        /// <summary> Descriptive name of the document. </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary> Classification (Policy, Procedure, Form, etc.). </summary>
        public Enums.DocumentCategory Category { get; set; } = Enums.DocumentCategory.Other;

        /// <summary> Relative or absolute path to the stored file. </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary> Name or ID of the user who uploaded the file. </summary>
        public string UploadedBy { get; set; } = string.Empty;

        /// <summary> Date and time of upload. </summary>
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        /// <summary> Document version string (e.g., "1.0", "Rev B"). </summary>
        public string Version { get; set; } = "1.0";

        /// <summary> Display-friendly file size (e.g. "2.5 MB"). </summary>
        public string FileSize { get; set; } = "0 KB";


    }
}
