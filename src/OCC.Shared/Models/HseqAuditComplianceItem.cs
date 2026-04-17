using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a specific verified compliance point within an HSEQ audit.
    /// Documents adherence to safety regulations or positive findings.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>HseqAuditComplianceItems</c> table.
    /// <b>How:</b> Linked to a parent <see cref="HseqAudit"/>. Often includes photographic evidence.
    /// </remarks>
    public class HseqAuditComplianceItem : BaseEntity
    {


        /// <summary> Foreign Key linking to the parent <see cref="HseqAudit"/>. </summary>
        public Guid AuditId { get; set; }

        /// <summary> Description of the compliant condition or observation. </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary> Reference code for the applicable regulation (e.g., "OHS Act 85 of 1993, GAR 9"). </summary>
        public string RegulationReference { get; set; } = string.Empty;

        /// <summary> Base64 encoded string of the supporting photo. </summary>
        public string PhotoBase64 { get; set; } = string.Empty;
        

    }
}
