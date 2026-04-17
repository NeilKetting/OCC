using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a statutory public holiday or company-observed non-working day.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>PublicHolidays</c> table.
    /// <b>How:</b> Used by payroll logic to determine overtime rates or paid time off entitlements.
    /// </remarks>
    public class PublicHoliday : BaseEntity
    {

        
        /// <summary> The calendar date of the holiday. </summary>
        public DateTime Date { get; set; }

        /// <summary> The name or occasion (e.g., "Freedom Day"). </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary> Computed year property for easy filtering (e.g. <c>GetHolidays(2025)</c>). </summary>
        public int Year => Date.Year;
    }
}
