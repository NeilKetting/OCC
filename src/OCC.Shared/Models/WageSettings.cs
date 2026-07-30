using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// System-wide customizable configuration settings for wage calculations.
    /// Allows payroll administrators to adjust rates, shift defaults, and calculation rules.
    /// </summary>
    public class WageSettings : BaseEntity
    {
        /// <summary> Default pay frequency for Cape Town branch ("Weekly" or "Fortnightly"). </summary>
        public PayFrequency CptDefaultPayFrequency { get; set; } = PayFrequency.Weekly;

        /// <summary> Default pay frequency for Johannesburg branch ("Fortnightly" or "Weekly"). </summary>
        public PayFrequency JhbDefaultPayFrequency { get; set; } = PayFrequency.Fortnightly;

        /// <summary> Standard shift cutoff day of week for weekly pay runs (default Wednesday). </summary>
        public DayOfWeek WeeklyShiftCutoffDay { get; set; } = DayOfWeek.Wednesday;

        /// <summary> Rate per day worked for Cape Town BIBC registered employees. </summary>
        public decimal BibcRatePerDay { get; set; } = 28.75m;

        /// <summary> Default supervisor incentive fee amount. </summary>
        public decimal DefaultSupervisorFee { get; set; } = 500.00m;

        /// <summary> Default company housing washing fee deduction amount. </summary>
        public decimal DefaultCompanyHousingWashingFee { get; set; } = 0m;

        /// <summary> Default shift start time (e.g. 07:00:00). </summary>
        public TimeSpan DefaultShiftStartTime { get; set; } = new TimeSpan(7, 0, 0);

        /// <summary> Default shift end time (e.g. 17:00:00). </summary>
        public TimeSpan DefaultShiftEndTime { get; set; } = new TimeSpan(17, 0, 0);

        /// <summary> Hour threshold after which 1 hour lunch deduction applies (default 13 = 13:00). </summary>
        public int LunchEndHourThreshold { get; set; } = 13;

        /// <summary> Whether to deduct lunch on Saturday overtime. </summary>
        public bool DeductLunchOnSaturday { get; set; } = false;

        /// <summary> Whether to deduct lunch on Sunday overtime. </summary>
        public bool DeductLunchOnSunday { get; set; } = false;

        /// <summary> Whether to deduct lunch on Public Holiday overtime. </summary>
        public bool DeductLunchOnPublicHoliday { get; set; } = false;

        /// <summary> Whether to enable Thursday/Friday projected hours on draft runs. </summary>
        public bool EnableProjectedHours { get; set; } = true;

        /// <summary> Whether to automatically recover prior ad-hoc "mamparra" advances on subsequent standard runs. </summary>
        public bool AutoRecoverAdHocAdvances { get; set; } = true;
    }
}
