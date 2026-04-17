using System;
using System.Collections.Generic;
using OCC.Shared.Enums;

namespace OCC.Shared.DTOs
{
    public class AuditSummaryDto
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string AuditNumber { get; set; } = string.Empty;
        public AuditStatus Status { get; set; }
        public string HseqConsultant { get; set; } = string.Empty;
        public decimal TargetScore { get; set; }
        public decimal ActualScore { get; set; }
    }

    public class AuditDto
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string ScopeOfWorks { get; set; } = string.Empty;
        public string SiteManager { get; set; } = string.Empty;
        public string SiteSupervisor { get; set; } = string.Empty;
        public string HseqConsultant { get; set; } = string.Empty;
        public string AuditNumber { get; set; } = string.Empty;
        public decimal TargetScore { get; set; }
        public decimal ActualScore { get; set; }
        public AuditStatus Status { get; set; }
        public DateTime? CloseOutDate { get; set; }
        public List<AuditSectionDto> Sections { get; set; } = new();
        public List<AuditNonComplianceItemDto> NonComplianceItems { get; set; } = new();
        public List<AuditAttachmentDto> Attachments { get; set; } = new();
        public byte[]? RowVersion { get; set; }
    }

    public class AuditSectionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal PossibleScore { get; set; }
        public decimal ActualScore { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class AuditNonComplianceItemDto
    {
        public Guid Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string RegulationReference { get; set; } = string.Empty;
        public string CorrectiveAction { get; set; } = string.Empty;
        public string ResponsiblePerson { get; set; } = string.Empty;
        public DateTime? TargetDate { get; set; }
        public AuditItemStatus Status { get; set; }
        public DateTime? ClosedDate { get; set; }
        public List<AuditAttachmentDto> Attachments { get; set; } = new();
        public byte[]? RowVersion { get; set; }
    }

    public class AuditAttachmentDto
    {
        public Guid Id { get; set; }
        public Guid? NonComplianceItemId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }
}
