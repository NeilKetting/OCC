using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.WpfClient.Features.ProjectHub.Models
{
    public partial class MilestoneReportItem : ObservableObject
    {
        public Guid TaskId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime PlannedDate { get; set; }
        public DateTime StartDate { get; set; }
        public int Progress { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsComplete { get; set; }

        public bool IsOverdue => !IsComplete && PlannedDate.Date < DateTime.Today;

        [ObservableProperty]
        private string _reason = string.Empty;
    }
}
