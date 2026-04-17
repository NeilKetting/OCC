using System;
using System.Collections.Generic;

namespace OCC.Shared.DTOs
{
    public class ProjectSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string ProjectManager { get; set; } = string.Empty;
        public int Progress { get; set; }
        public DateTime? LatestFinish { get; set; }
        public DateTime StartDate { get; set; }
        public int TaskCount { get; set; }
        public Guid? SiteManagerId { get; set; }
    }

    public class ProjectReportDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Budget { get; set; }

        public decimal TotalMaterialCost { get; set; }
        public decimal TotalLabourCost { get; set; }
        public decimal TotalProjectCost => TotalMaterialCost + TotalLabourCost;

        public System.Collections.Generic.List<OrderSummaryDto> LinkedOrders { get; set; } = new();
        public System.Collections.Generic.List<LabourDetailDto> LabourBreakdown { get; set; } = new();
    }

    public class LabourDetailDto
    {
        public string EmployeeName { get; set; } = string.Empty;
        public double Hours { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal TotalCost => (decimal)Hours * HourlyRate;
    }
}
