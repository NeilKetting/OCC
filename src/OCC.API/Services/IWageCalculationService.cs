using OCC.Shared.Models;

namespace OCC.API.Services
{
    /// <summary>
    /// Calculates the hours breakdown for a single attendance record against
    /// a specific employee's shift configuration and the active
    /// <see cref="WageCalculationOptions"/> rules.
    /// </summary>
    public interface IWageCalculationService
    {
        /// <summary>
        /// Computes Normal, OT 1.5×, OT 2.0×, and Lunch deduction hours
        /// for a single attendance record.
        /// </summary>
        /// <param name="record">The attendance record to evaluate.</param>
        /// <param name="employee">The employee whose shift bounds are used.</param>
        /// <param name="settings">Optional DB WageSettings overrides.</param>
        /// <returns>
        /// A <see cref="HoursBreakdown"/> containing all computed values.
        /// </returns>
        HoursBreakdown CalculateHours(AttendanceRecord record, Employee employee, WageSettings? settings = null);
    }
}
