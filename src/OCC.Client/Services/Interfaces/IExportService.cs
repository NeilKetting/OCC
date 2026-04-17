using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface IExportService
    {
        Task ExportToCsvAsync<T>(IEnumerable<T> data, string filePath);
        Task<string> GenerateHtmlReportAsync<T>(IEnumerable<T> data, string title, Dictionary<string, string> columns);
        Task<string> GenerateIndividualStaffReportAsync<T>(OCC.Shared.Models.Employee employee, DateTime start, DateTime end, IEnumerable<T> data, Dictionary<string, string> summary);
        Task<string> GenerateEmployeeProfileHtmlAsync(OCC.Shared.Models.Employee employee);
        Task<string> GenerateAuditDeviationReportAsync(OCC.Shared.Models.HseqAudit audit, IEnumerable<OCC.Shared.Models.HseqAuditNonComplianceItem> items);
        Task OpenFileAsync(string filePath);
    }
}
