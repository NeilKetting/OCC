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
        public HoursBreakdown CalculateHours(AttendanceRecord record, Employee employee, WageSettings? settings = null)
        {
            var options = settings != null ? ToOptions(settings) : _options;
            DateTime recDate = record.Date.Kind == DateTimeKind.Utc ? record.Date.ToLocalTime().Date : record.Date.Date;
            bool isHoliday = HolidayUtils.IsPublicHoliday(recDate);

            // ── Public Holiday Handling ──────────────────────────────────────────────
            if (isHoliday)
            {
                if (record.Status == AttendanceStatus.UnpaidSick || record.Status == AttendanceStatus.UnpaidLeave)
                {
                    return new HoursBreakdown(record.PaidLeaveHours ?? 0, 0, 0, 0);
                }

                if (record.Status == AttendanceStatus.Absent || record.Status == AttendanceStatus.Sick || record.Status == AttendanceStatus.LeaveAuthorized)
                {
                    double holidayNormal = GetStandardDailyHours(employee, options);
                    double normalHours = (record.PaidLeaveHours.HasValue && record.PaidLeaveHours.Value > 0) ? record.PaidLeaveHours.Value : holidayNormal;
                    return new HoursBreakdown(normalHours, 0, 0, 0);
                }

                if (record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late || record.Status == AttendanceStatus.LeaveEarly)
                {
                    double holidayStandard = GetStandardDailyHours(employee, options);
                    if (record.CheckInTime != null && record.CheckOutTime != null)
                    {
                        DateTime startDt = record.CheckInTime.Value;
                        DateTime endDt   = record.CheckOutTime.Value;
                        double totalDur = (endDt - startDt).TotalHours;
                        if (totalDur > 0)
                        {
                            bool deductLunch = options.DeductLunchOnPublicHoliday;
                            double lunch = deductLunch ? ComputeWeekdayLunch(endDt, record.Date, options) : 0.0;
                            double extraPaid = record.PaidLeaveHours ?? 0;
                            return new HoursBreakdown(extraPaid, 0, totalDur - lunch, lunch);
                        }
                    }

                    return new HoursBreakdown(record.PaidLeaveHours ?? 0, 0, holidayStandard, 0);
                }
            }

            // If they didn't work at all and have explicit leave hours, return early
            if (record.CheckInTime == null && record.PaidLeaveHours.HasValue)
            {
                return new HoursBreakdown(record.PaidLeaveHours.Value, 0, 0, 0);
            }

            // Paid leave check (only if they do not have the explicit PaidLeaveHours field): Sick or LeaveAuthorized
            if (!record.PaidLeaveHours.HasValue && (record.Status == AttendanceStatus.Sick || record.Status == AttendanceStatus.LeaveAuthorized))
            {
                double leaveNormal = GetStandardDailyHours(employee, options);
                return new HoursBreakdown(leaveNormal, 0, 0, 0);
            }

            // Guard: no check-in or absent/unpaid sick/unpaid leave → nothing to pay (unless they have explicit PaidLeaveHours).
            if (record.CheckInTime == null || record.Status == AttendanceStatus.Absent || record.Status == AttendanceStatus.UnpaidSick || record.Status == AttendanceStatus.UnpaidLeave)
            {
                return new HoursBreakdown(record.PaidLeaveHours ?? 0, 0, 0, 0);
            }

            // Guard: no check-out → can't compute duration
            if (record.CheckOutTime == null)
            {
                double paidHours = record.PaidLeaveHours ?? 0;
                double standardNormal = GetStandardDailyHours(employee, options);

                if (record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late || record.Status == AttendanceStatus.LeaveEarly)
                {
                    if (isHoliday)
                    {
                        return new HoursBreakdown(paidHours, 0, standardNormal, 0);
                    }

                    if (record.Date.Date == DateTime.Today)
                    {
                        var recordDow = record.Date.DayOfWeek;
                        bool isRecordSunday = recordDow == DayOfWeek.Sunday;
                        bool isRecordSaturday = recordDow == DayOfWeek.Saturday;

                        if (isRecordSunday || isRecordSaturday)
                        {
                            return new HoursBreakdown(paidHours, 0, 0, 0);
                        }

                        return new HoursBreakdown(standardNormal + paidHours, 0, 0, 0);
                    }
                }

                return new HoursBreakdown(paidHours, 0, 0, 0);
            }

            DateTime start = record.CheckInTime.Value;
            DateTime end   = record.CheckOutTime.Value;

            double totalDuration = (end - start).TotalHours;
            if (totalDuration <= 0)
            {
                if (isHoliday)
                {
                    if (record.Status == AttendanceStatus.Absent)
                        return new HoursBreakdown(GetStandardDailyHours(employee, options), 0, 0, 0);
                    if (record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late || record.Status == AttendanceStatus.LeaveEarly)
                        return new HoursBreakdown(record.PaidLeaveHours ?? 0, 0, GetStandardDailyHours(employee, options), 0);
                }
                return new HoursBreakdown(record.PaidLeaveHours ?? 0, 0, 0, 0);
            }

            // ── Classify the day ──────────────────────────────────────────────
            var  dow       = record.Date.DayOfWeek;
            bool isSunday  = dow == DayOfWeek.Sunday;
            bool isSaturday = dow == DayOfWeek.Saturday;

            // ── Public Holiday → 2.0× for Present employees ──────────────────
            if (isHoliday)
            {
                if (record.Status == AttendanceStatus.Absent)
                {
                    return new HoursBreakdown(GetStandardDailyHours(employee, options), 0, 0, 0);
                }

                bool deductLunch = options.DeductLunchOnPublicHoliday;
                double lunch = deductLunch
                    ? ComputeWeekdayLunch(end, record.Date, options)
                    : 0.0;

                double extraPaid = record.PaidLeaveHours ?? 0;
                return new HoursBreakdown(extraPaid, 0, totalDuration - lunch, lunch);
            }

            // ── Sunday → 2.0× ──────────────────────────────
            if (isSunday)
            {
                bool deductLunch = options.DeductLunchOnSunday;
                double lunch = deductLunch
                    ? ComputeWeekdayLunch(end, record.Date, options)
                    : 0.0;

                double extraPaid = record.PaidLeaveHours ?? 0;
                return new HoursBreakdown(extraPaid, 0, totalDuration - lunch, lunch);
            }

            // ── Saturday → 1.5× ────────────────────────────────────
            if (isSaturday)
            {
                double lunch = options.DeductLunchOnSaturday
                    ? ComputeWeekdayLunch(end, record.Date, options)
                    : 0.0;

                double extraPaid = record.PaidLeaveHours ?? 0;
                return new HoursBreakdown(extraPaid, totalDuration - lunch, 0, lunch);
            }

            // ── Weekday ───────────────────────────────────────────────────────
            double lunchDeduction = options.UseLunchEndThreshold
                ? ComputeWeekdayLunch(end, record.Date, options)
                : 0.0;

            // Shift bounds for this employee
            TimeSpan shiftStart = employee.ShiftStartTime ?? options.DefaultShiftStart;
            TimeSpan shiftEnd   = employee.ShiftEndTime   ?? options.DefaultShiftEnd;

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

        private double GetStandardDailyHours(Employee employee, WageCalculationOptions options)
        {
            TimeSpan shiftStart = employee.ShiftStartTime ?? options.DefaultShiftStart;
            TimeSpan shiftEnd;
            if (employee.ShiftEndTime.HasValue)
            {
                shiftEnd = employee.ShiftEndTime.Value;
            }
            else if (string.Equals(employee.Branch, "Cape Town", StringComparison.OrdinalIgnoreCase) || string.Equals(employee.Branch, "CPT", StringComparison.OrdinalIgnoreCase))
            {
                shiftEnd = new TimeSpan(16, 30, 0);
            }
            else if (string.Equals(employee.Branch, "Johannesburg", StringComparison.OrdinalIgnoreCase) || string.Equals(employee.Branch, "JHB", StringComparison.OrdinalIgnoreCase))
            {
                shiftEnd = new TimeSpan(16, 45, 0);
            }
            else
            {
                shiftEnd = options.DefaultShiftEnd;
            }

            double normal = (shiftEnd - shiftStart).TotalHours;
            if (options.UseLunchEndThreshold && shiftEnd.Hours >= 13)
            {
                normal -= 1.0;
            }

            if (normal < 0) normal = 0;
            return normal;
        }

        private WageCalculationOptions ToOptions(WageSettings settings)
        {
            return new WageCalculationOptions
            {
                LunchStartHour = _options.LunchStartHour,
                LunchEndHour = settings.LunchEndHourThreshold,
                DeductLunchOnSaturday = settings.DeductLunchOnSaturday,
                DeductLunchOnSunday = settings.DeductLunchOnSunday,
                DeductLunchOnPublicHoliday = settings.DeductLunchOnPublicHoliday,
                UseLunchEndThreshold = true,
                SaturdayOtMultiplier = _options.SaturdayOtMultiplier,
                SundayHolidayOtMultiplier = _options.SundayHolidayOtMultiplier,
                DefaultShiftStart = _options.DefaultShiftStart,
                DefaultShiftEnd = _options.DefaultShiftEnd,
                DefaultProjectedDailyHours = _options.DefaultProjectedDailyHours
            };
        }

        private double ComputeWeekdayLunch(DateTime checkOut, DateTime recordDate, WageCalculationOptions options)
        {
            DateTime lunchEnd = recordDate.Date.AddHours(options.LunchEndHour);
            return checkOut >= lunchEnd ? (options.LunchEndHour - options.LunchStartHour) : 0.0;
        }
    }
}
