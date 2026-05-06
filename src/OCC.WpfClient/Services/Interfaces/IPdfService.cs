using OCC.Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IPdfService
    {
        Task<string> GenerateOrderPdfAsync(Order order, bool isPrintVersion = false);
        
        /// <summary>
        /// Generates a branded list report from a collection of items.
        /// </summary>
        Task<string> GenerateListReportPdfAsync<T>(string title, IEnumerable<T> items, List<ReportColumnDefinition> columns);
        
        /// <summary>
        /// Generates a branded profile/detail report for a single entity.
        /// </summary>
        Task<string> GenerateDetailReportPdfAsync<T>(string title, T item);
    }

    public class ReportColumnDefinition
    {
        public string Header { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public double Width { get; set; } = 1.0; // Relative width
    }
}
