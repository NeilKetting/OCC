using System;
using System.Collections.Generic;
using OCC.Shared.Enums;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents an unplanned event or near-miss on a project site that requires investigation.
    /// This includes injuries, property damage, or environmental incidents.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>Incidents</c> table.
    /// <b>How:</b> Incidents are reported via the mobile or desktop apps, tracked by <see cref="Severity"/>, 
    /// and should eventually have a <see cref="RootCause"/> and <see cref="CorrectiveAction"/> assigned.
    /// </remarks>
    public class Incident : BaseEntity
    {


        /// <summary> The date and time the incident occurred. </summary>
        public DateTime Date { get; set; } = DateTime.UtcNow;

        /// <summary> The category of incident (e.g., Medical, Near Miss, Damaged Equipment). </summary>
        public IncidentType Type { get; set; }

        /// <summary> The classification of impact (e.g., Minor, Medium, Major, Fatal). </summary>
        public IncidentSeverity Severity { get; set; }

        /// <summary> Detailed location description where it occurred (e.g., "Site A, Sector 4"). </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary> A narrative description of what happened. </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary> Identification of the user who initially registered the report. </summary>
        public string ReportedByUserId { get; set; } = string.Empty;

        /// <summary> Current administrative status (Open, Investigating, Closed). </summary>
        public IncidentStatus Status { get; set; } = IncidentStatus.Open;
        
        /// <summary> Identification of the person assigned to handle the investigation. </summary>
        public string InvestigatorId { get; set; } = string.Empty;

        /// <summary> The underlying reason for the incident as determined by investigation. </summary>
        public string RootCause { get; set; } = string.Empty;

        /// <summary> Steps taken or required to prevent recurrence. </summary>
        public string CorrectiveAction { get; set; } = string.Empty;

        /// <summary> Photographic evidence or relevant images attached to the incident. </summary>
        public List<IncidentPhoto> Photos { get; set; } = new();

        /// <summary> Supporting documents attached to the incident. </summary>
        public List<IncidentDocument> Documents { get; set; } = new();


    }
}

