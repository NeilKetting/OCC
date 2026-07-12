using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IProjectReportService
    {
        Task<ProjectReportDraft?> GetDraftAsync(Guid projectId);
        Task<bool> SaveDraftAsync(Guid projectId, ProjectReportDraft draft);
        Task<IEnumerable<ProjectReportHistory>> GetHistoryAsync(Guid projectId);
        Task<ProjectReportHistory?> UploadReportAsync(Guid projectId, int weekNumber, string reportName, Stream fileStream, string fileName);
        Task<string> UploadReportPhotoAsync(Stream fileStream, string fileName);
    }
}
