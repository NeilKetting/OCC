using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.DTOs;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface IHealthSafetyService
    {
        // Incidents
        Task<IEnumerable<IncidentSummaryDto>> GetIncidentsAsync();
        Task<IncidentDto?> GetIncidentAsync(Guid id);
        Task<IncidentDto?> CreateIncidentAsync(Incident incident);
        Task<bool> UpdateIncidentAsync(Incident incident);
        Task<bool> DeleteIncidentAsync(Guid id);
        Task<IncidentPhotoDto?> UploadIncidentPhotoAsync(IncidentPhoto metadata, System.IO.Stream fileStream, string fileName);
        Task<bool> DeleteIncidentPhotoAsync(Guid id);
        Task<IncidentDocumentDto?> UploadIncidentDocumentAsync(Guid incidentId, System.IO.Stream fileStream, string fileName);
        Task<bool> DeleteIncidentDocumentAsync(Guid id);

        // Audits
        Task<IEnumerable<AuditSummaryDto>> GetAuditsAsync();
        Task<AuditDto?> GetAuditAsync(Guid id);
        Task<AuditDto?> CreateAuditAsync(AuditDto audit);
        Task<bool> UpdateAuditAsync(AuditDto audit);
        Task<bool> DeleteAuditAsync(Guid id);
        Task<IEnumerable<AuditNonComplianceItemDto>> GetAuditDeviationsAsync(Guid auditId);
        Task<AuditAttachmentDto?> UploadAuditAttachmentAsync(HseqAuditAttachment metadata, System.IO.Stream fileStream, string fileName);
        Task<bool> DeleteAuditAttachmentAsync(Guid id);

        // Training
        Task<IEnumerable<HseqTrainingSummaryDto>> GetTrainingSummariesAsync();
        Task<IEnumerable<HseqTrainingRecord>> GetTrainingRecordsAsync();
        Task<HseqTrainingRecord?> GetTrainingRecordAsync(Guid id);
        Task<IEnumerable<HseqTrainingSummaryDto>> GetExpiringTrainingAsync(int days);
        Task<HseqTrainingRecord?> CreateTrainingRecordAsync(HseqTrainingRecord record);
        Task<bool> UpdateTrainingRecordAsync(HseqTrainingRecord record);
        Task<string?> UploadCertificateAsync(System.IO.Stream fileStream, string fileName);
        Task<bool> DeleteTrainingRecordAsync(Guid id);

        // Documents
        Task<IEnumerable<HseqDocument>> GetDocumentsAsync();
        Task<HseqDocument?> UploadDocumentAsync(HseqDocument metadata, System.IO.Stream fileStream, string fileName);
        Task<bool> DeleteDocumentAsync(Guid id);

        // Stats
        Task<HseqDashboardStats?> GetDashboardStatsAsync();
        Task<IEnumerable<HseqSafeHourRecord>> GetPerformanceHistoryAsync(int? year = null);
    }

    public class HseqDashboardStats
    {
        public double TotalSafeHours { get; set; }
        public int IncidentsTotal { get; set; }
        public int NearMisses { get; set; }
        public int Injuries { get; set; }
        public int Environmentals { get; set; }
        public List<AuditScoreDto> RecentAuditScores { get; set; } = new();
    }

    public class AuditScoreDto
    {
        public string SiteName { get; set; } = string.Empty;
        public decimal ActualScore { get; set; }
        public DateTime Date { get; set; }
    }
}
