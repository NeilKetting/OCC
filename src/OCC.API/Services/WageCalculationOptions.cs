namespace OCC.API.Services
{
    /// <summary>
    /// Configuration flags that govern how hours and deductions are computed
    /// during a wage run. Values are loaded from <c>appsettings.json</c>
    /// ("WageCalculation" section) so rules can be tweaked without a code change.
    /// </summary>
    public class WageCalculationOptions
    {
        // ── Lunch Window ─────────────────────────────────────────────────────

        /// <summary>Hour (24-h) at which the unpaid lunch break starts. Default 12.</summary>
        public int LunchStartHour { get; set; } = 12;

        /// <summary>Hour (24-h) at which the unpaid lunch break ends. Default 13.</summary>
        public int LunchEndHour { get; set; } = 13;

        // ── Lunch Deduction Rules ─────────────────────────────────────────────

        /// <summary>
        /// Deduct lunch on Saturday shifts.
        /// Client rule: <c>false</c> — full hours are paid on Saturdays.
        /// </summary>
        public bool DeductLunchOnSaturday { get; set; } = false;

        /// <summary>
        /// Deduct lunch on Sunday shifts.
        /// Client rule: <c>false</c> — full hours are paid on Sundays.
        /// </summary>
        public bool DeductLunchOnSunday { get; set; } = false;

        /// <summary>
        /// Deduct lunch on Public Holiday shifts.
        /// Client rule: <c>true</c> — deduct 1 hour lunch on public holidays if checkout >= 13:00.
        /// </summary>
        public bool DeductLunchOnPublicHoliday { get; set; } = true;

        // ── Weekday Lunch Threshold Rule ──────────────────────────────────────

        /// <summary>
        /// On weekdays, lunch is only deducted when the employee's checkout time
        /// is at or after <see cref="LunchEndHour"/> (13:00).
        /// If the employee leaves before the lunch period ends, no deduction is made.
        /// </summary>
        public bool UseLunchEndThreshold { get; set; } = true;

        // ── OT Rate Multipliers ───────────────────────────────────────────────

        /// <summary>Saturday overtime multiplier. Default 1.5×.</summary>
        public decimal SaturdayOtMultiplier { get; set; } = 1.5m;

        /// <summary>Sunday / Public Holiday overtime multiplier. Default 2.0×.</summary>
        public decimal SundayHolidayOtMultiplier { get; set; } = 2.0m;

        // ── Shift Defaults ────────────────────────────────────────────────────

        /// <summary>
        /// Default shift start time used when an employee has no shift configured.
        /// Default 07:00.
        /// </summary>
        public System.TimeSpan DefaultShiftStart { get; set; } = new System.TimeSpan(7, 0, 0);

        /// <summary>
        /// Default shift end time used when an employee has no shift configured.
        /// Default 16:00.
        /// </summary>
        public System.TimeSpan DefaultShiftEnd { get; set; } = new System.TimeSpan(16, 0, 0);

        /// <summary>
        /// Fallback daily hours for projected (future) days when no employee shift is defined.
        /// Default 9.0 h.
        /// </summary>
        public double DefaultProjectedDailyHours { get; set; } = 9.0;
    }
}
