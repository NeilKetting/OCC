using System;
using System.Collections.Generic;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a payroll calculation period (e.g., Weekly or Bi-Weekly wages).
    /// Aggregates attendance and rate data to determine amounts due to employees.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>WageRuns</c> table.
    /// <b>How:</b> Contains multiple <see cref="WageRunLine"/> entries, one per employee. 
    /// Can be in Draft state for review before being Finalized.
    /// </remarks>
    public class WageRun : BaseEntity
    {

        
        /// <summary> The start date of the pay period (inclusive). </summary>
        public DateTime StartDate { get; set; }

        /// <summary> The end date of the pay period (inclusive). </summary>
        public DateTime EndDate { get; set; }

        /// <summary> The date this wage run was generated. </summary>
        public DateTime RunDate { get; set; } = DateTime.UtcNow;
        
        /// <summary> Current state: Draft, Finalized, or Paid. </summary>
        public WageRunStatus Status { get; set; } = WageRunStatus.Draft;

        /// <summary> Administrative notes regarding this pay run. </summary>
        public string? Notes { get; set; }
        
        /// <summary> Which RateType this wage run applies to (e.g., Hourly or MonthlySalary). If null or empty, assumes Hourly for backward compatibility. </summary>
        public string? PayType { get; set; }
        
        /// <summary> Which Branch this wage run applies to (e.g., Johannesburg, Cape Town, or All). </summary>
        public string? Branch { get; set; }

        /// <summary> Collection of individual employee wage calculations for this run. </summary>
        public List<WageRunLine> Lines { get; set; } = new();

        /// <summary> Used for inputting the total gas charge to split among housed employees. </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal InputTotalGasCharge { get; set; }

        /// <summary> Used for inputting the default supervisor fee to apply to supervisors. </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal InputDefaultSupervisorFee { get; set; }

        /// <summary> Used for inputting a flat washing fee for employees in company housing. </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal InputCompanyHousingWashingFee { get; set; }

        // IEntity Implementation - Replaced by BaseEntity

    }

    /// <summary>
    /// Status of the payroll processing workflow.
    /// </summary>
    public enum WageRunStatus
    {
        /// <summary> Initial calculation, open for editing and review. </summary>
        Draft,
        /// <summary> Confirmed and locked. No further changes allowed. </summary>
        Finalized,
        /// <summary> Payment has been processed/disbursed. </summary>
        Paid
    }
}
