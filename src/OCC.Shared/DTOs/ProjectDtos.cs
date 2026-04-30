using System;
using System.Collections.Generic;

namespace OCC.Shared.DTOs
{
    public class ProjectSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        private string _status = "Active";
        public string Status 
        { 
            get 
            {
                if (Progress >= 100 && _status != "Archived" && _status != "OnHold" && _status != "Cancelled")
                    return "Completed";
                
                if (Progress > 0 && (_status == "Planning" || _status == "Not Started"))
                    return "In Progress";
                    
                return _status;
            }
            set => _status = value;
        }
        public string ProjectManager { get; set; } = string.Empty;
        public int Progress { get; set; }
        public DateTime? LatestFinish { get; set; }
        public DateTime StartDate { get; set; }
        public int TaskCount { get; set; }
        public Guid? SiteManagerId { get; set; }
        public string SiteManagerName { get; set; } = string.Empty;
    }

    public class ProjectPersonnelDto
    {
        public Guid ProjectId { get; set; }
        public Guid? SiteManagerId { get; set; }
        public string? SiteManagerName { get; set; }
        public string? ProjectManager { get; set; }
        public List<EmployeeSummaryDto> TeamMembers { get; set; } = new();
    }

    public class ProjectPersonnelUpdateDto
    {
        public Guid? SiteManagerId { get; set; }
        // ProjectManager is now read-only (creator)
        public List<Guid> TeamMemberIds { get; set; } = new();
    }

    public class ProjectHistoryDto
    {
        public Guid ProjectId { get; set; }
        public List<PersonnelHistoryEntryDto> Entries { get; set; } = new();
    }

    public class PersonnelHistoryEntryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Type { get; set; } = "Staff"; // Staff or Contractor
        public int TasksAssigned { get; set; }
        public int DaysWorked { get; set; }
        public DateTime? FirstActive { get; set; }
        public DateTime? LastActive { get; set; }
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
        public Guid? ProjectId { get; set; }

        public string Message
        {
            get
            {
                string userName = !string.IsNullOrEmpty(DisplayName) ? DisplayName : User;
                string projectSuffix = !string.IsNullOrEmpty(ProjectName) ? $" at [{ProjectName}]" : "";

                if (string.IsNullOrEmpty(Status))
                    return $"{Action} '{TaskName}'{projectSuffix} by {userName}.";
                
                string formattedStatus = Status;
                if (formattedStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                    return $"Task '{TaskName}'{projectSuffix} completed by {userName}.";
                if (formattedStatus.Equals("Started", StringComparison.OrdinalIgnoreCase))
                    return $"Task '{TaskName}'{projectSuffix} started by {userName}.";
                
                return $"Task '{TaskName}'{projectSuffix} progress: {Status} (updated by {userName}).";
            }
        }

        public string StatusColor
        {
            get
            {
                if (string.IsNullOrEmpty(Status)) return "#A0FFFFFF";

                return Status switch
                {
                    "Not Started" or "To Do" => "#A0FFFFFF", // Grey (TextSub)
                    "Started" or "In Progress" => "#2E9DFF", // Blue (AccentBlue)
                    "Halfway" => "#8B5CF6", // Violet
                    "Almost Done" => "#EC4899", // Pink
                    "Done" or "Completed" => "#00C853", // Green (SuccessGreen)
                    "On Hold" => "#FFC107", // Yellow (SecondaryGold)
                    _ => "#A0FFFFFF"
                };
            }
        }
    }
}
