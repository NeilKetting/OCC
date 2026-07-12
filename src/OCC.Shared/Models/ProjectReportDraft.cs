using System;

namespace OCC.Shared.Models
{
    public class ProjectReportDraft : BaseEntity
    {
        public Guid ProjectId { get; set; }
        public virtual Project? Project { get; set; }

        public string StatusSummary { get; set; } = string.Empty;
        public string GeneralWasteTon { get; set; } = "0";
        public string RubbleM3 { get; set; } = "0";
        public string ScrapMetalsTon { get; set; } = "0";
        public string AsbestosTon { get; set; } = "0";

        public DateTime? SiteEstablishmentPlanned { get; set; }
        public DateTime? SiteEstablishmentActual { get; set; }
        public DateTime? PracticalCompletionPlanned { get; set; }
        public DateTime? PracticalCompletionActual { get; set; }

        public double PowPercentRequired { get; set; }
        public int DelayDays { get; set; }

        public DateTime? StreamingPlanned { get; set; }
        public DateTime? StreamingActual { get; set; }

        public string OverdueMilestoneReasons { get; set; } = string.Empty;
        public string PhotoUrls { get; set; } = string.Empty;
    }
}
