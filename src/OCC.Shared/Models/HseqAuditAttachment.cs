using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents an attachment (image or document) linked to an HSEQ Audit or a specific Non-Compliance Item.
    /// </summary>
    public class HseqAuditAttachment : BaseEntity
    {


        public Guid AuditId { get; set; }

        /// <summary>
        /// Optional link to a specific deviation item. If null, it's an overall audit attachment.
        /// </summary>
        public Guid? NonComplianceItemId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string FileSize { get; set; } = string.Empty;

        public string UploadedBy { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("AuditId")]
        public virtual HseqAudit? Audit { get; set; }

        [ForeignKey("NonComplianceItemId")]
        public virtual HseqAuditNonComplianceItem? NonComplianceItem { get; set; }
    }
}
