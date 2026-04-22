using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OCC.Shared.Models
{
    public enum SnagStatus
    {
        Open,
        InProgress,
        Fixed,
        Verified,
        Closed
    }

    public enum SnagSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class SnagJob : BaseEntity
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public SnagStatus Status { get; set; } = SnagStatus.Open;
        public SnagSeverity Severity { get; set; } = SnagSeverity.Medium;

        // Relations
        [Required]
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }

        public Guid? OriginalTaskId { get; set; }
        public ProjectTask? OriginalTask { get; set; }

        [Required]
        public Guid SubContractorId { get; set; }
        public SubContractor? SubContractor { get; set; }

        // Timeline
        public DateTime? DueDate { get; set; }
        public DateTime? CompletionDate { get; set; }

        // Metadata for calculation
        public bool FixedOnTime => CompletionDate.HasValue && DueDate.HasValue && CompletionDate.Value <= DueDate.Value;
    }
}
