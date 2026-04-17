using System;
using System.Collections.Generic;
using OCC.Shared.Enums;

namespace OCC.Shared.DTOs
{
    public class IncidentPhotoDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    public class IncidentDocumentDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    public class IncidentSummaryDto
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public string Location { get; set; } = string.Empty;
        public IncidentStatus Status { get; set; }
        public string ReportedByUserId { get; set; } = string.Empty;
        public int PhotoCount { get; set; }
        public int DocumentCount { get; set; }
    }

    public class IncidentDto
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ReportedByUserId { get; set; } = string.Empty;
        public IncidentStatus Status { get; set; }
        public string InvestigatorId { get; set; } = string.Empty;
        public string RootCause { get; set; } = string.Empty;
        public string CorrectiveAction { get; set; } = string.Empty;
        public List<IncidentPhotoDto> Photos { get; set; } = new();
        public List<IncidentDocumentDto> Documents { get; set; } = new();
        public byte[]? RowVersion { get; set; }
    }
}
