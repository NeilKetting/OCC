using System;
using System.ComponentModel.DataAnnotations.Schema;

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

        /// <summary> Cost of the variation order. </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }

        /// <summary> Claim amount associated with the variation order. </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ClaimAmount { get; set; }

        public string Status { get; set; } = "Variation Request";
        public bool IsInvoiced { get; set; }
        public int DurationDays { get; set; }
    }
}
