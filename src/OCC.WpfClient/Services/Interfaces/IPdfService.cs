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

        /// <summary>
        /// Generates a branded replica Project Hub report.
        /// </summary>
        Task<string> GenerateProjectReportPdfAsync(ProjectReportPrintModel model);

        /// <summary>
        /// Generates a printable portrait A4 leave application form with OCC branding and employee/manager signature blocks.
        /// </summary>
        Task<string> GenerateLeaveFormPdfAsync(OCC.Shared.Models.LeaveRequest request);
    }

    public class ReportColumnDefinition
    {
        public string Header { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public double Width { get; set; } = 1.0; // Relative width
    }

    public class MilestonePrintModel
    {
        public string Name { get; set; } = string.Empty;
        public System.DateTime StartDate { get; set; }
        public System.DateTime PlannedDate { get; set; }
        public int Progress { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
    }

    public class ProjectReportPrintModel
    {
        public Project Project { get; set; } = new();
        public System.DateTime ReportDate { get; set; } = System.DateTime.Today;
        public int WeekNumber { get; set; }
        public int TotalTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int CompletedTasks { get; set; }
        public double OverallProgress { get; set; }
        public double PowPercentRequired { get; set; }
        public int DelayDays { get; set; }
        public double SafeWorkingHours { get; set; }
        public List<MilestonePrintModel> ThisWeekMilestones { get; set; } = new();
        public List<MilestonePrintModel> OverdueMilestones { get; set; } = new();
        public string GeneralWasteTon { get; set; } = "0";
        public string RubbleM3 { get; set; } = "0";
        public string ScrapMetalsTon { get; set; } = "0";
        public string AsbestosTon { get; set; } = "0";
        public string StatusSummary { get; set; } = string.Empty;
        public List<ProjectReportPrintVendorRow> VendorReportRows { get; set; } = new();
        public List<ProjectVariationOrder> VariationOrders { get; set; } = new();
        public List<string> IncidentPhotoPaths { get; set; } = new();
        public string? CustomerLogoPath { get; set; }
    }

    public class ProjectReportPrintVendorRow
    {
        public string VendorName { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string SafetyApproved { get; set; } = "Yes";
        public string AppScore { get; set; } = "100%";
        public string Audit1 { get; set; } = string.Empty;
        public string Audit2 { get; set; } = string.Empty;
        public string Audit3 { get; set; } = string.Empty;
    }
}
