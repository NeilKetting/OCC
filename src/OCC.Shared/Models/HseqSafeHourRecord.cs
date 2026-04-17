using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Captures aggregated safety performance metrics for a specific period (usually monthly).
    /// Used to calculate LTIFR (Lost Time Injury Frequency Rate) and Safe Work Hours statistics.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>HseqSafeHourRecords</c> table.
    /// <b>How:</b> Aggregates data from <see cref="AttendanceRecord"/> (total hours) and <see cref="Incident"/> reports.
    /// </remarks>
    public class HseqSafeHourRecord : BaseEntity
    {


        /// <summary> The reporting month (usually set to the 1st of the month). </summary>
        public DateTime Month { get; set; }

        /// <summary> Total man-hours worked without a lost-time injury. </summary>
        public double SafeWorkHours { get; set; }

        /// <summary> Text indicator or ID reference if an incident occurred ("Yes", "No", or IncidentGuid). </summary>
        public string IncidentReported { get; set; } = string.Empty;

        /// <summary> Count of near-miss incidents reported in this period. </summary>
        public int NearMisses { get; set; }

        /// <summary> Summary of root causes for any incidents/near-misses. </summary>
        public string RootCause { get; set; } = string.Empty;

        /// <summary> Summary of corrective actions taken to address issues. </summary>
        public string CorrectiveActions { get; set; } = string.Empty;

        /// <summary> Status of the reporting period (e.g., Open, Finalized). </summary>
        public string Status { get; set; } = "Open";

        /// <summary> Name of the person verifying these figures. </summary>
        public string ReportedBy { get; set; } = string.Empty;


    }
}
