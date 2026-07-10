using Microsoft.Extensions.Options;
using OCC.Shared.Models;
using OCC.Shared.Utils;
using System;

namespace OCC.API.Services
{
    /// <summary>
    /// Core wage-calculation engine. Extracted from <c>WageRunsController</c>
    /// so it can be unit-tested in isolation and configured via
    /// <see cref="WageCalculationOptions"/>.
    /// </summary>
    /// <remarks>
    /// <b>Lunch deduction rules (as confirmed by client):</b>
    /// <list type="bullet">
    ///   <item>Saturday — NO lunch deduction. Full hours paid at 1.5×.</item>
    ///   <item>Sunday   — NO lunch deduction. Full hours paid at 2.0×.</item>
    ///   <item>Public Holiday — NO lunch deduction. Full hours paid at 2.0×.</item>
    ///   <item>Weekday — Deduct 1 hour ONLY if the employee's checkout time is
    ///     at or after the lunch-end hour (default 13:00). If they leave before
    ///     13:00 no deduction is applied.</item>
    /// </list>
    /// </remarks>
    public class WageCalculationService : IWageCalculationService
    {
        private readonly WageCalculationOptions _options;

        public WageCalculationService(IOptions<WageCalculationOptions> options)
        {
            _options = options.Value;
        }

        /// <summary>
        /// Constructor that accepts options directly — used in unit tests that
        /// don't have the full DI container wired up.
        /// </summary>
        public WageCalculationService(WageCalculationOptions options)
        {
            _options = options;
        }

        /// <inheritdoc/>
        public HoursBreakdown CalculateHours(AttendanceRecord record, Employee employee)
        {
            // If they didn't work at all and have explicit leave hours, return early
            if (record.CheckInTime == null && record.PaidLeaveHours.HasValue)
            {
                return new HoursBreakdown(record.PaidLeaveHours.Value, 0, 0, 0);
            }

            // Paid leave check (only if they do not have the explicit PaidLeaveHours field): Sick or LeaveAuthorized
            if (!record.PaidLeaveHours.HasValue && (record.Status == AttendanceStatus.Sick || record.Status == AttendanceStatus.LeaveAuthorized))
            {
                // Shift bounds for this employee
                TimeSpan leaveShiftStart = employee.ShiftStartTime ?? _options.DefaultShiftStart;
                TimeSpan leaveShiftEnd   = employee.ShiftEndTime   ?? _options.DefaultShiftEnd;

                double leaveNormal = (leaveShiftEnd - leaveShiftStart).TotalHours;
                if (_options.UseLunchEndThreshold && leaveShiftEnd.Hours >= 13)
                {
                    leaveNormal -= 1.0;
                }

                if (leaveNormal < 0) leaveNormal = 0;
                return new HoursBreakdown(leaveNormal, 0, 0, 0);
            }

            // Guard: no check-in or absent/unpaid sick/unpaid leave → nothing to pay (unless they have explicit PaidLeaveHours)
            if (record.CheckInTime == null || record.Status == AttendanceStatus.Absent || record.Status == AttendanceStatus.UnpaidSick || record.Status == AttendanceStatus.UnpaidLeave)
            {
                return new HoursBreakdown(record.PaidLeaveHours ?? 0, 0, 0, 0);
            }

            // Guard: no check-out → can't compute duration
            if (record.CheckOutTime == null)
            {
                double paidHours = record.PaidLeaveHours ?? 0;
                if (record.Date.Date == DateTime.Today && 
                    record.CheckInTime != null && 
                    (record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late || record.Status == AttendanceStatus.LeaveEarly))
                {
                    var recordDow = record.Date.DayOfWeek;
                    bool isRecordSunday = recordDow == DayOfWeek.Sunday;
                    bool isRecordSaturday = recordDow == DayOfWeek.Saturday;
                    bool isRecordHoliday = HolidayUtils.IsPublicHoliday(record.Date);

                    if (isRecordSunday || isRecordSaturday || isRecordHoliday)
                    {
                        return new HoursBreakdown(paidHours, 0, 0, 0);
                    }

                    TimeSpan standardShiftStart = employee.ShiftStartTime ?? _options.DefaultShiftStart;
                    TimeSpan standardShiftEnd   = employee.ShiftEndTime   ?? _options.DefaultShiftEnd;

                    double standardNormal = (standardShiftEnd - standardShiftStart).TotalHours;
                    if (_options.UseLunchEndThreshold && standardShiftEnd.Hours >= 13)
                    {
                        standardNormal -= 1.0;
                    }

                    if (standardNormal < 0) standardNormal = 0;
                    return new HoursBreakdown(standardNormal + paidHours, 0, 0, 0);
                }

                return new HoursBreakdown(paidHours, 0, 0, 0);
            }

            DateTime start = record.CheckInTime.Value;
            DateTime end   = record.CheckOutTime.Value;

            double totalDuration = (end - start).TotalHours;
            if (totalDuration <= 0)
                return new HoursBreakdown(record.PaidLeaveHours ?? 0, 0, 0, 0);

            // ── Classify the day ──────────────────────────────────────────────
            var  dow       = record.Date.DayOfWeek;
            bool isSunday  = dow == DayOfWeek.Sunday;
            bool isSaturday = dow == DayOfWeek.Saturday;
            bool isHoliday = HolidayUtils.IsPublicHoliday(record.Date);

            // ── Sunday / Public Holiday → 2.0×, NO lunch ─────────────────────
            if (isSunday || isHoliday)
            {
                double lunch = _options.DeductLunchOnSunday || _options.DeductLunchOnPublicHoliday
                    ? ComputeWeekdayLunch(end, record.Date)
                    : 0.0;

                double extraPaid = record.PaidLeaveHours ?? 0;
                return new HoursBreakdown(extraPaid, 0, totalDuration - lunch, lunch);
            }

            // ── Saturday → 1.5×, NO lunch ────────────────────────────────────
            if (isSaturday)
            {
                double lunch = _options.DeductLunchOnSaturday
                    ? ComputeWeekdayLunch(end, record.Date)
                    : 0.0;

                double extraPaid = record.PaidLeaveHours ?? 0;
                return new HoursBreakdown(extraPaid, totalDuration - lunch, 0, lunch);
            }

            // ── Weekday ───────────────────────────────────────────────────────
            // Lunch rule: deduct 1 h ONLY if checkout is at or after LunchEndHour.
            double lunchDeduction = _options.UseLunchEndThreshold
                ? ComputeWeekdayLunch(end, record.Date)
                : 0.0;

            // Shift bounds for this employee
            TimeSpan shiftStart = employee.ShiftStartTime ?? _options.DefaultShiftStart;
            TimeSpan shiftEnd   = employee.ShiftEndTime   ?? _options.DefaultShiftEnd;

            DateTime shiftStartDt = record.Date.Date.Add(shiftStart);
            DateTime shiftEndDt   = record.Date.Date.Add(shiftEnd);

            // Normal hours = overlap of actual time with shift window, minus lunch
            double normal = 0;
            DateTime overlapStart = start > shiftStartDt ? start : shiftStartDt;
            DateTime overlapEnd   = end   < shiftEndDt   ? end   : shiftEndDt;

            if (overlapStart < overlapEnd)
            {
                normal = (overlapEnd - overlapStart).TotalHours - lunchDeduction;
                if (normal < 0) normal = 0;
            }

            // OT 1.5× = any time worked outside shift bounds (before or after), minus lunch already deducted from normal
            double overtime15 = totalDuration - lunchDeduction - normal;
            if (overtime15 < 0) overtime15 = 0;

            double extraLeave = record.PaidLeaveHours ?? 0;
            return new HoursBreakdown(normal + extraLeave, overtime15, 0, lunchDeduction);
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Returns 1.0 if the employee's checkout is at or after the configured
        /// lunch-end hour; otherwise 0.0.
        /// This implements the client rule: "If they work till 13:00 and leave
        /// we deduct the hour. If they leave before 12:00 we do not deduct."
        /// </summary>
        private double ComputeWeekdayLunch(DateTime checkOut, DateTime recordDate)
        {
            DateTime lunchEnd = recordDate.Date.AddHours(_options.LunchEndHour);
            return checkOut >= lunchEnd ? (_options.LunchEndHour - _options.LunchStartHour) : 0.0;
        }
    }
}
