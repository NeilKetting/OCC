using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a daily aggregate payroll record for an employee (V2 System).
    /// Tracks total hours, expected wages, and status for a given day.
    /// This is separate from physical clocking events.
    /// </summary>
    public class DailyTimesheet : BaseEntity
    {
        /// <summary> Foreign key to the <see cref="Employee"/> record. </summary>
        public Guid EmployeeId { get; set; }

        /// <summary> The calendar date of the timesheet. </summary>
        public DateTime Date { get; set; }

        /// <summary> The first clock-in event of the day, derived from ClockingEvents. </summary>
        public DateTime? FirstInTime { get; set; }

        /// <summary> The last clock-out event of the day, derived from ClockingEvents. </summary>
        public DateTime? LastOutTime { get; set; }

        /// <summary> The payroll status (Present, Absent, Sick, Leave, AutoPaid, Late). </summary>
        public TimesheetStatus Status { get; set; } = TimesheetStatus.Present;

        /// <summary> Total calculated working hours for the day based on physical events or auto-pay rules. </summary>
        public decimal CalculatedHours { get; set; }

        /// <summary> Estimated wage for the day based on CalculatedHours and the employee's rate. </summary>
        public decimal WageEstimated { get; set; }

        /// <summary> Indicates if there is a missing clock-out event for this timesheet. </summary>
        public bool HasMissingClockOut { get; set; }

        /// <summary> Indicates if a manager manually edited this timesheet. </summary>
        public bool IsManualOverride { get; set; }

        /// <summary> General comments or explanations for manager edits/auto-generation. </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Defines various states for the daily timesheet payroll record.
    /// </summary>
    public enum TimesheetStatus
    {
        Present,
        Absent,
        Late,
        Sick,
        LeaveEarly,
        LeaveAuthorized,
        UnpaidSick,
        AutoPaid
    }
}
