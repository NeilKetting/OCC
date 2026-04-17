namespace OCC.Shared.Enums
{
    /// <summary>
    /// Categorizes the nature of a reported safety incident.
    /// </summary>
    public enum IncidentType
    {
        /// <summary> Physical harm to a person. </summary>
        Injury,
        /// <summary> An unplanned event that did not result in injury, illness, or damage - but had the potential to do so. </summary>
        NearMiss,
        /// <summary> Damage to equipment, vehicles, or structures. </summary>
        PropertyDamage,
        /// <summary> Spills, leaks, or other ecological impact. </summary>
        Environmental,
        /// <summary> Fire or explosion events. </summary>
        Fire,
        /// <summary> Stolen assets or materials. </summary>
        Theft,
        /// <summary> Any other unclassified incident. </summary>
        Other
    }

    /// <summary>
    /// Classifies the potential or actual impact of an incident.
    /// </summary>
    public enum IncidentSeverity
    {
        /// <summary> Minor issue requiring little to no intervention (e.g. First Aid only). </summary>
        Low,
        /// <summary> Moderate impact, medical treatment or significant repair required. </summary>
        Medium,
        /// <summary> Serious impact, Lost Time Injury (LTI) or major damage. </summary>
        High,
        /// <summary> Death of an individual. </summary>
        Fatality,
        /// <summary> Catastrophic failure or imminent extreme danger. </summary>
        Critical
    }

    /// <summary>
    /// Tracks the administrative lifecycle of an incident report.
    /// </summary>
    public enum IncidentStatus
    {
        /// <summary> Report created but not yet processed. </summary>
        Open,
        /// <summary> Assigned to an HSEQ officer for root cause analysis. </summary>
        Investigating,
        /// <summary> Investigation complete and corrective actions implemented. </summary>
        Closed,
        /// <summary> Historical record. </summary>
        Archived
    }

    /// <summary>
    /// Lifecycle stages of a safety audit.
    /// </summary>
    public enum AuditStatus
    {
        /// <summary> Draft or initial setup. </summary>
        Open,
        /// <summary> Planned for a future date. </summary>
        Scheduled,
        /// <summary> Currently being conducted on site. </summary>
        InProgress,
        /// <summary> Data collection finished. </summary>
        Completed,
        /// <summary> Reviewed and finalized by management. </summary>
        Closed
    }

    /// <summary>
    /// Status of a specific finding (Non-Compliance) within an audit.
    /// </summary>
    public enum AuditItemStatus
    {
        /// <summary> Issue identified and outstanding. </summary>
        Open,
        /// <summary> Corrective action taken (waiting verification). </summary>
        Rectified,
        /// <summary> Verified as resolved. </summary>
        Closed
    }

    /// <summary>
    /// Classification for HSEQ documentation types.
    /// </summary>
    public enum DocumentCategory
    {
        /// <summary> Official company rules and guidelines. </summary>
        Policy,
        /// <summary> Blank documents for reuse (checklists, forms). </summary>
        Template,
        /// <summary> Step-by-step Standard Operating Procedures (SOPs). </summary>
        Procedure,
        /// <summary> Completed or fillable records. </summary>
        Form,
        /// <summary> Generated output or statistical analysis. </summary>
        Report,
        /// <summary> Uncategorized files. </summary>
        Other
    }
}
