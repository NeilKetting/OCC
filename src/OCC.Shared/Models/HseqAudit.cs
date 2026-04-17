using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a Health, Safety, Environment, and Quality (HSEQ) audit performed at a site.
    /// Used for safety compliance tracking and reporting.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>HseqAudits</c> table.
    /// <b>How:</b> Linked to <see cref="HseqAuditSection"/> and various compliance items. 
    /// Tracks scoring against a <see cref="TargetScore"/> to determine safety health.
    /// </remarks>
    public class HseqAudit : BaseEntity
    {


        /// <summary> The date the audit was conducted. </summary>
        public DateTime Date { get; set; }

        /// <summary> Name of the site or project where the audit occurred. </summary>
        public string SiteName { get; set; } = string.Empty;

        /// <summary> Description of the work scope being audited. </summary>
        public string ScopeOfWorks { get; set; } = string.Empty;

        /// <summary> Name of the Site Manager at the time of audit. </summary>
        public string SiteManager { get; set; } = string.Empty;

        /// <summary> Name of the Site Supervisor at the time of audit. </summary>
        public string SiteSupervisor { get; set; } = string.Empty;

        /// <summary> The HSEQ professional conducting the audit. </summary>
        public string HseqConsultant { get; set; } = string.Empty;

        /// <summary> A unique reference number for the audit report. </summary>
        public string AuditNumber { get; set; } = string.Empty;

        /// <summary> The maximum possible score for this audit. </summary>
        public decimal TargetScore { get; set; }

        /// <summary> The actual achieved score based on compliance. </summary>
        public decimal ActualScore { get; set; }


        /// <summary> Current status of the audit (e.g., Scheduled, InProgress, Completed). </summary>
        public Enums.AuditStatus Status { get; set; } = Enums.AuditStatus.Scheduled;

        /// <summary> The date all actions from this audit were finalized. </summary>
        public DateTime? CloseOutDate { get; set; }

        /// <summary> Logical sections of the audit (e.g., Tooling, PPE, Documentation). </summary>
        public System.Collections.Generic.List<HseqAuditSection> Sections { get; set; } = new();

        /// <summary> Specific items that were found to be compliant. </summary>
        public System.Collections.Generic.List<HseqAuditComplianceItem> ComplianceItems { get; set; } = new();

        /// <summary> Specific items that failed compliance checks. </summary>
        public System.Collections.Generic.List<HseqAuditNonComplianceItem> NonComplianceItems { get; set; } = new();

        /// <summary> Attachments linked to this audit. </summary>
        public System.Collections.Generic.List<HseqAuditAttachment> Attachments { get; set; } = new();


    }
}

