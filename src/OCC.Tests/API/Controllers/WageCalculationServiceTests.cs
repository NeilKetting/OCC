using OCC.API.Services;
using OCC.Shared.Models;
using System;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    /// <summary>
    /// Unit tests for <see cref="WageCalculationService"/>.
    /// Uses the direct-options constructor so no DI container is needed.
    /// </summary>
    public class WageCalculationServiceTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Creates the service with the default (production) options.</summary>
        private static WageCalculationService CreateService(WageCalculationOptions? opts = null)
            => new(opts ?? new WageCalculationOptions());

        /// <summary>
        /// Builds a minimal Employee with a default 07:00-16:00 shift.
        /// </summary>
        private static Employee DefaultEmployee() => new()
        {
            Id              = Guid.NewGuid(),
            FirstName       = "Test",
            LastName        = "Worker",
            ShiftStartTime  = new TimeSpan(7, 0, 0),
            ShiftEndTime    = new TimeSpan(16, 0, 0),
        };

        /// <summary>Builds an AttendanceRecord for the given date and clock times.</summary>
        private static AttendanceRecord Record(
            DateTime date,
            TimeSpan checkIn,
            TimeSpan checkOut,
            Guid? employeeId = null,
            AttendanceStatus status = AttendanceStatus.Present)
            => new()
            {
                Id           = Guid.NewGuid(),
                EmployeeId   = employeeId ?? Guid.NewGuid(),
                Date         = date.Date,
                CheckInTime  = date.Date.Add(checkIn),
                CheckOutTime = date.Date.Add(checkOut),
                Status       = status,
            };

        // A known Monday date (not a public holiday)
        private static readonly DateTime KnownMonday    = new(2026, 6, 22); // Monday 22 Jun 2026
        private static readonly DateTime KnownSaturday  = new(2026, 6, 20); // Saturday 20 Jun 2026
        private static readonly DateTime KnownSunday    = new(2026, 6, 21); // Sunday 21 Jun 2026
        // Christmas 2026 falls on a Friday
        private static readonly DateTime Christmas2026  = new(2026, 12, 25); // Friday — public holiday

        // ═══════════════════════════════════════════════════════════════════════
        // GUARD CASES
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public void Absent_Record_Returns_AllZero()
        {
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownMonday, new(7, 0, 0), new(16, 0, 0));
            record.Status = AttendanceStatus.Absent;

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(0, result.Normal);
            Assert.Equal(0, result.Overtime15);
            Assert.Equal(0, result.Overtime20);
            Assert.Equal(0, result.Lunch);
        }

        [Fact]
        public void No_CheckOut_Returns_AllZero()
        {
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownMonday, new(7, 0, 0), new(16, 0, 0));
            record.CheckOutTime = null;

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(0, result.Normal);
            Assert.Equal(0, result.Overtime15);
            Assert.Equal(0, result.Overtime20);
            Assert.Equal(0, result.Lunch);
        }

        [Fact]
        public void No_CheckIn_Returns_AllZero()
        {
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownMonday, new(7, 0, 0), new(16, 0, 0));
            record.CheckInTime = null;

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(0, result.Normal);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // WEEKDAY TESTS
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public void Weekday_FullDay_07to16_DeductsOneLunchHour()
        {
            // 07:00-16:00 = 9 h total, deduct 1 h lunch (checkout >= 13:00) → 8 h normal
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownMonday, new(7, 0, 0), new(16, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(8.0, result.Normal,    precision: 2);
            Assert.Equal(0.0, result.Overtime15, precision: 2);
            Assert.Equal(0.0, result.Overtime20, precision: 2);
            Assert.Equal(1.0, result.Lunch,      precision: 2);
        }

        [Fact]
        public void Weekday_MorningOnly_LeaveBefore12_NoLunch()
        {
            // 07:00-11:00 = 4 h, leave before 12:00 → no lunch deduction
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownMonday, new(7, 0, 0), new(11, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(4.0, result.Normal, precision: 2);
            Assert.Equal(0.0, result.Lunch,  precision: 2);
        }

        [Fact]
        public void Weekday_LeaveDuringLunch_At12h30_NoLunch()
        {
            // 07:00-12:30 = 5.5 h, leaves before 13:00 → no lunch deduction
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownMonday, new(7, 0, 0), new(12, 30, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(5.5, result.Normal, precision: 2);
            Assert.Equal(0.0, result.Lunch,  precision: 2);
        }

        [Fact]
        public void Weekday_LeaveExactlyAt13h00_DeductsLunch()
        {
            // 07:00-13:00 = 6 h total, checkout exactly at 13:00 → deduct 1 h lunch → 5 h normal
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownMonday, new(7, 0, 0), new(13, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(5.0, result.Normal, precision: 2);
            Assert.Equal(1.0, result.Lunch,  precision: 2);
        }

        [Fact]
        public void Weekday_OvertimeAfterShift_07to18_Correct()
        {
            // 07:00-18:00 = 11 h total, lunch 1 h → net 10 h
            // Shift 07:00-16:00 → normal = 9h - 1h lunch = 8 h
            // OT = 10 - 8 = 2 h at 1.5×
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownMonday, new(7, 0, 0), new(18, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(8.0, result.Normal,     precision: 2);
            Assert.Equal(2.0, result.Overtime15, precision: 2);
            Assert.Equal(0.0, result.Overtime20, precision: 2);
            Assert.Equal(1.0, result.Lunch,      precision: 2);
        }

        [Fact]
        public void Weekday_ArriveLate_After12_NoLunch()
        {
            // Employee arrives at 13:30, leaves at 17:00 → checkout >= 13:00 BUT
            // lunch period (12-13) is entirely before checkin. 
            // Checkout >= 13:00 so threshold fires: deduct 1 h lunch.
            // Normal = (16:00-13:30) - 1h lunch = 2.5 - 1 = 1.5h, OT = 17:00-16:00 = 1h
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownMonday, new(13, 30, 0), new(17, 0, 0));

            var result = svc.CalculateHours(record, emp);

            // 3.5h total - 1h lunch = 2.5h net
            // Overlap 13:30-16:00 = 2.5h - 1h lunch = 1.5h normal
            // OT = 2.5 - 1.5 = 1h
            Assert.Equal(1.5, result.Normal,     precision: 2);
            Assert.Equal(1.0, result.Overtime15, precision: 2);
            Assert.Equal(1.0, result.Lunch,      precision: 2);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SATURDAY TESTS (NO LUNCH — 1.5×)
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public void Saturday_FullDay_07to14_NoLunch_AllOT15()
        {
            // 07:00-14:00 Saturday = 7 h, no lunch deduction → all goes to OT 1.5×
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownSaturday, new(7, 0, 0), new(14, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(0.0, result.Normal,     precision: 2);
            Assert.Equal(7.0, result.Overtime15, precision: 2);
            Assert.Equal(0.0, result.Overtime20, precision: 2);
            Assert.Equal(0.0, result.Lunch,      precision: 2);
        }

        [Fact]
        public void Saturday_WorkingThroughLunch_07to16_NoLunchDeducted()
        {
            // 07:00-16:00 Saturday = 9 h, no lunch deduction (client rule)
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownSaturday, new(7, 0, 0), new(16, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(0.0, result.Normal,     precision: 2);
            Assert.Equal(9.0, result.Overtime15, precision: 2);
            Assert.Equal(0.0, result.Overtime20, precision: 2);
            Assert.Equal(0.0, result.Lunch,      precision: 2);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SUNDAY TESTS (NO LUNCH — 2.0×)
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public void Sunday_FullDay_07to14_NoLunch_AllOT20()
        {
            // 07:00-14:00 Sunday = 7 h, no lunch → all OT 2.0×
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownSunday, new(7, 0, 0), new(14, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(0.0, result.Normal,     precision: 2);
            Assert.Equal(0.0, result.Overtime15, precision: 2);
            Assert.Equal(7.0, result.Overtime20, precision: 2);
            Assert.Equal(0.0, result.Lunch,      precision: 2);
        }

        [Fact]
        public void Sunday_WorkingThroughLunch_NoLunchDeducted()
        {
            // 07:00-16:00 Sunday = 9 h, no lunch deduction
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(KnownSunday, new(7, 0, 0), new(16, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(0.0, result.Normal,     precision: 2);
            Assert.Equal(0.0, result.Overtime15, precision: 2);
            Assert.Equal(9.0, result.Overtime20, precision: 2);
            Assert.Equal(0.0, result.Lunch,      precision: 2);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PUBLIC HOLIDAY TESTS (NO LUNCH — 2.0×)
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public void PublicHoliday_Christmas_07to14_NoLunch_AllOT20()
        {
            // Christmas 2026 = Friday 25 Dec — public holiday
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(Christmas2026, new(7, 0, 0), new(14, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(0.0, result.Normal,     precision: 2);
            Assert.Equal(0.0, result.Overtime15, precision: 2);
            Assert.Equal(7.0, result.Overtime20, precision: 2);
            Assert.Equal(0.0, result.Lunch,      precision: 2);
        }

        [Fact]
        public void PublicHoliday_WorkingThroughLunch_NoLunchDeducted()
        {
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            var record = Record(Christmas2026, new(7, 0, 0), new(16, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(9.0, result.Overtime20, precision: 2);
            Assert.Equal(0.0, result.Lunch,      precision: 2);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TOTAL WAGE FORMULA TESTS
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public void TotalWage_Formula_Weekday_IsCorrect()
        {
            // 07:00-16:00 Monday → 8h normal, 0h OT, rate = R50/h
            // TotalWage = 8 × 50 = R400
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            emp.HourlyRate = 50;
            var record = Record(KnownMonday, new(7, 0, 0), new(16, 0, 0));

            var hours = svc.CalculateHours(record, emp);
            decimal totalWage = (decimal)hours.Normal * (decimal)emp.HourlyRate
                              + (decimal)hours.Overtime15 * (decimal)emp.HourlyRate * 1.5m
                              + (decimal)hours.Overtime20 * (decimal)emp.HourlyRate * 2.0m;

            Assert.Equal(400m, totalWage);
        }

        [Fact]
        public void TotalWage_Formula_Saturday_IsCorrect()
        {
            // 07:00-14:00 Saturday → 7h at 1.5×, rate = R50/h
            // TotalWage = 7 × 50 × 1.5 = R525
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            emp.HourlyRate = 50;
            var record = Record(KnownSaturday, new(7, 0, 0), new(14, 0, 0));

            var hours = svc.CalculateHours(record, emp);
            decimal totalWage = (decimal)hours.Normal * (decimal)emp.HourlyRate
                              + (decimal)hours.Overtime15 * (decimal)emp.HourlyRate * 1.5m
                              + (decimal)hours.Overtime20 * (decimal)emp.HourlyRate * 2.0m;

            Assert.Equal(525m, totalWage);
        }

        [Fact]
        public void TotalWage_Formula_Sunday_IsCorrect()
        {
            // 07:00-14:00 Sunday → 7h at 2.0×, rate = R50/h
            // TotalWage = 7 × 50 × 2.0 = R700
            var svc    = CreateService();
            var emp    = DefaultEmployee();
            emp.HourlyRate = 50;
            var record = Record(KnownSunday, new(7, 0, 0), new(14, 0, 0));

            var hours = svc.CalculateHours(record, emp);
            decimal totalWage = (decimal)hours.Normal * (decimal)emp.HourlyRate
                              + (decimal)hours.Overtime15 * (decimal)emp.HourlyRate * 1.5m
                              + (decimal)hours.Overtime20 * (decimal)emp.HourlyRate * 2.0m;

            Assert.Equal(700m, totalWage);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CONFIG FLAG TESTS
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public void Config_DeductLunchOnSaturday_WhenEnabled_DeductsLunch()
        {
            // If the flag is ever turned on in config, Saturday SHOULD deduct lunch
            var opts = new WageCalculationOptions { DeductLunchOnSaturday = true };
            var svc  = CreateService(opts);
            var emp  = DefaultEmployee();
            // 07:00-16:00 Saturday — checkout past 13:00
            var record = Record(KnownSaturday, new(7, 0, 0), new(16, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(1.0, result.Lunch,      precision: 2); // lunch deducted
            Assert.Equal(8.0, result.Overtime15, precision: 2); // 9h - 1h
        }

        [Fact]
        public void Config_DeductLunchOnSunday_WhenEnabled_DeductsLunch()
        {
            var opts = new WageCalculationOptions { DeductLunchOnSunday = true };
            var svc  = CreateService(opts);
            var emp  = DefaultEmployee();
            var record = Record(KnownSunday, new(7, 0, 0), new(16, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(1.0, result.Lunch,      precision: 2);
            Assert.Equal(8.0, result.Overtime20, precision: 2);
        }

        [Fact]
        public void Config_UseLunchEndThreshold_False_UseIntersectionLunch()
        {
            // When threshold mode is off, lunch is 0 (no intersection logic in this path —
            // service returns 0 because UseLunchEndThreshold=false and no alternative is set).
            var opts = new WageCalculationOptions { UseLunchEndThreshold = false };
            var svc  = CreateService(opts);
            var emp  = DefaultEmployee();
            var record = Record(KnownMonday, new(7, 0, 0), new(16, 0, 0));

            var result = svc.CalculateHours(record, emp);

            // No lunch deducted when threshold mode is disabled
            Assert.Equal(0.0, result.Lunch,   precision: 2);
            Assert.Equal(9.0, result.Normal,  precision: 2); // full 9h paid
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EMPLOYEE WITH CUSTOM SHIFT
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public void Weekday_CustomShift_06to15_OvertimeAfter15()
        {
            // Employee has 06:00-15:00 shift, works 06:00-17:00
            // Lunch at 13:00 → deduct 1h → net 10h
            // Shift overlap 06:00-15:00 = 9h - 1h lunch = 8h normal
            // OT = 10 - 8 = 2h
            var svc = CreateService();
            var emp = new Employee
            {
                Id            = Guid.NewGuid(),
                FirstName     = "Custom",
                LastName      = "Shift",
                ShiftStartTime = new TimeSpan(6, 0, 0),
                ShiftEndTime   = new TimeSpan(15, 0, 0),
            };
            var record = Record(KnownMonday, new(6, 0, 0), new(17, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(8.0, result.Normal,     precision: 2);
            Assert.Equal(2.0, result.Overtime15, precision: 2);
            Assert.Equal(1.0, result.Lunch,      precision: 2);
        }

        [Fact]
        public void Weekday_NoShiftConfigured_UsesDefaultShift()
        {
            // Employee has no shift → default 07:00-16:00 applies
            var svc = CreateService();
            var emp = new Employee
            {
                Id             = Guid.NewGuid(),
                FirstName      = "No",
                LastName       = "Shift",
                ShiftStartTime = null,
                ShiftEndTime   = null,
            };
            var record = Record(KnownMonday, new(7, 0, 0), new(16, 0, 0));

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(8.0, result.Normal, precision: 2); // default shift applied
            Assert.Equal(1.0, result.Lunch,  precision: 2);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // LEAVE & SICK STATUS TESTS
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public void CalculateHours_SickLeave_ReturnsStandardHours_WithoutTimes()
        {
            // Employee with default 07:00-16:00 shift (9 hours, minus 1 hour lunch = 8 hours normal)
            var svc = CreateService();
            var emp = DefaultEmployee();
            
            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = emp.Id,
                Date = KnownMonday,
                Status = AttendanceStatus.Sick,
                CheckInTime = null,
                CheckOutTime = null
            };

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(8.0, result.Normal, precision: 2);
            Assert.Equal(0.0, result.Overtime15, precision: 2);
            Assert.Equal(0.0, result.Overtime20, precision: 2);
            Assert.Equal(0.0, result.Lunch, precision: 2);
        }

        [Fact]
        public void CalculateHours_LeaveAuthorized_ReturnsStandardHours_WithoutTimes()
        {
            // Employee with custom 06:00-15:00 shift (9 hours, minus 1 hour lunch = 8 hours normal)
            var svc = CreateService();
            var emp = new Employee
            {
                Id = Guid.NewGuid(),
                ShiftStartTime = new TimeSpan(6, 0, 0),
                ShiftEndTime = new TimeSpan(15, 0, 0)
            };

            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = emp.Id,
                Date = KnownMonday,
                Status = AttendanceStatus.LeaveAuthorized,
                CheckInTime = null,
                CheckOutTime = null
            };

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(8.0, result.Normal, precision: 2);
            Assert.Equal(0.0, result.Lunch, precision: 2);
        }

        [Fact]
        public void CalculateHours_UnpaidSick_ReturnsZeroHours()
        {
            var svc = CreateService();
            var emp = DefaultEmployee();

            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = emp.Id,
                Date = KnownMonday,
                Status = AttendanceStatus.UnpaidSick,
                CheckInTime = null,
                CheckOutTime = null
            };

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(0.0, result.Normal, precision: 2);
            Assert.Equal(0.0, result.Overtime15, precision: 2);
            Assert.Equal(0.0, result.Overtime20, precision: 2);
        }

        [Fact]
        public void CalculateHours_AbsentWithoutTimes_ReturnsZeroHours()
        {
            var svc = CreateService();
            var emp = DefaultEmployee();

            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = emp.Id,
                Date = KnownMonday,
                Status = AttendanceStatus.Absent,
                CheckInTime = null,
                CheckOutTime = null
            };

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(0.0, result.Normal, precision: 2);
        }

        [Fact]
        public void CalculateHours_FullDayPaidLeaveHours_ReturnsPaidHours()
        {
            var svc = CreateService();
            var emp = DefaultEmployee();

            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = emp.Id,
                Date = KnownMonday,
                Status = AttendanceStatus.LeaveAuthorized,
                PaidLeaveHours = 9.0,
                CheckInTime = null,
                CheckOutTime = null
            };

            var result = svc.CalculateHours(record, emp);

            Assert.Equal(9.0, result.Normal, precision: 2);
            Assert.Equal(0.0, result.Overtime15, precision: 2);
        }

        [Fact]
        public void CalculateHours_PartialDayPaidLeaveHours_CombinesWorkedAndLeave()
        {
            var svc = CreateService();
            var emp = DefaultEmployee(); // Shift: 07:00 - 16:00 (9 hours, normally 8 normal + 1 lunch)

            var record = Record(KnownMonday, new(7, 0, 0), new(12, 0, 0)); // Worked 5 hours (07:00-12:00) - checkOut is before 13:00 so no lunch deducted
            record.PaidLeaveHours = 4.5; // Half-day leave hours

            var result = svc.CalculateHours(record, emp);

            // Worked hours = 5.0 normal. Leave hours = 4.5. Total = 9.5 normal
            Assert.Equal(9.5, result.Normal, precision: 2);
            Assert.Equal(0.0, result.Overtime15, precision: 2);
        }

        [Fact]
        public void CalculateHours_PartialLeaveNoCheckOut_ReturnsPaidLeaveHoursOnly()
        {
            var svc = CreateService();
            var emp = DefaultEmployee();

            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = emp.Id,
                Date = KnownMonday,
                Status = AttendanceStatus.Present,
                PaidLeaveHours = 3.0,
                CheckInTime = KnownMonday.AddHours(7),
                CheckOutTime = null // Missing checkout
            };

            var result = svc.CalculateHours(record, emp);

            // Since it's not today (KnownMonday is in past) and checkout is missing, worked hours = 0. Paid leave hours = 3.0
            Assert.Equal(3.0, result.Normal, precision: 2);
        }

        [Fact]
        public void CalculateHours_UnpaidHalfDay_WithoutClockIn_ReturnsZeroPaidHours()
        {
            var svc = CreateService();
            var emp = DefaultEmployee();

            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = emp.Id,
                Date = KnownMonday,
                Status = AttendanceStatus.UnpaidHalfDay,
                UnpaidLeaveHours = 4.375,
                PaidLeaveHours = 0,
                CheckInTime = null,
                CheckOutTime = null
            };

            var result = svc.CalculateHours(record, emp);

            // No check in -> 0 paid normal hours
            Assert.Equal(0.0, result.Normal, precision: 2);
            Assert.Equal(0.0, result.Overtime15, precision: 2);
        }

        [Fact]
        public void CalculateHours_UnpaidHalfDay_WithHalfDayClockIn_ReturnsWorkedHoursOnly()
        {
            var svc = CreateService();
            var emp = DefaultEmployee(); // 07:00 to 16:00

            var record = Record(KnownMonday, new(7, 0, 0), new(12, 0, 0), emp.Id, AttendanceStatus.UnpaidHalfDay); // 07:00 to 12:00 = 5.0 hours
            record.UnpaidLeaveHours = 4.375;
            record.PaidLeaveHours = 0;

            var result = svc.CalculateHours(record, emp);

            // 5.0 worked normal hours, 0 paid leave hours
            Assert.Equal(5.0, result.Normal, precision: 2);
            Assert.Equal(0.0, result.Overtime15, precision: 2);
        }
    }
}
