using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IExportService
    {
        Task ExportToCsvAsync<T>(IEnumerable<T> data, string filePath);
        Task<string> GenerateHtmlReportAsync<T>(IEnumerable<T> data, string title, Dictionary<string, string> columns);
        Task<string> GenerateIndividualStaffReportAsync<T>(Employee employee, DateTime start, DateTime end, IEnumerable<T> data, Dictionary<string, string> summary);
        Task<string> GenerateEmployeeProfileHtmlAsync(Employee employee);
        Task<string> GenerateAuditDeviationReportAsync(HseqAudit audit, IEnumerable<HseqAuditNonComplianceItem> items);
        Task OpenFileAsync(string filePath);
    }
}
