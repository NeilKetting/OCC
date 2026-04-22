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

    public class DashboardUpdateDto
    {
        public DateTime Timestamp { get; set; }
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? DisplayName { get; set; }

        public string Message
        {
            get
            {
                string userName = !string.IsNullOrEmpty(DisplayName) ? DisplayName : User;

                if (string.IsNullOrEmpty(Status))
                    return $"{Action} '{TaskName}' by {userName}.";
                
                string formattedStatus = Status;
                if (formattedStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                    return $"Task '{TaskName}' completed by {userName}.";
                if (formattedStatus.Equals("Started", StringComparison.OrdinalIgnoreCase))
                    return $"Task '{TaskName}' started by {userName}.";
                
                return $"Task '{TaskName}' progress: {Status} (updated by {userName}).";
            }
        }
    }
}
