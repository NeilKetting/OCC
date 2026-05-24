using System;

namespace OCC.Shared.Models
{
    public class ProjectReportHistory : BaseEntity
    {
        public Guid ProjectId { get; set; }
        public virtual Project? Project { get; set; }

        public string ReportName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileSize { get; set; } = "0 KB";
        public int WeekNumber { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        public string GeneratedBy { get; set; } = string.Empty;
    }
}
