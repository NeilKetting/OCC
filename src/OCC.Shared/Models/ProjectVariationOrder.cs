using System;

namespace OCC.Shared.Models
{
    public class ProjectVariationOrder : BaseEntity
    {

        
        public Guid ProjectId { get; set; }
        public virtual Project? Project { get; set; }

        public string Description { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string AdditionalComments { get; set; } = string.Empty;

        public string Status { get; set; } = "Variation Request";
        public bool IsInvoiced { get; set; }
    }
}
