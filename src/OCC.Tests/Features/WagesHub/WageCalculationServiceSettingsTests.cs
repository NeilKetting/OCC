using OCC.API.Services;
using OCC.Shared.Models;
using System;
using Xunit;

namespace OCC.Tests.Features.WagesHub
{
    /// <summary>
    /// Exhaustive unit tests for WageCalculationService covering every WageSettings option and override combination.
    /// </summary>
    public class WageCalculationServiceSettingsTests
    {
        [Fact]
        public void LunchEndHourThreshold_WhenCheckoutIsBeforeThreshold_DoesNotDeductLunch()
        {
            // Arrange: Lunch threshold at 13 (13:00), employee checks out at 12:30
            var options = new WageCalculationOptions
            {
                UseLunchEndThreshold = true,
                LunchEndHour = 13,
                LunchStartHour = 12
            };
            var service = new WageCalculationService(options);

            var emp = new Employee
            {
                ShiftStartTime = new TimeSpan(7, 0, 0),
                ShiftEndTime = new TimeSpan(17, 0, 0)
            };

            var date = new DateTime(2026, 7, 27); // Monday
            var record = new AttendanceRecord
            {
                Date = date,
                CheckInTime = date.AddHours(7),
                CheckOutTime = date.AddHours(12).AddMinutes(30), // 12:30
                Status = AttendanceStatus.LeaveEarly
            };

            // Act
            var breakdown = service.CalculateHours(record, emp);

            // Assert: No lunch deducted because checkout < 13:00
            Assert.Equal(0.0, breakdown.Lunch);
            Assert.Equal(5.5, breakdown.Normal);
        }

        [Fact]
        public void LunchEndHourThreshold_WhenCheckoutIsAtOrAfterThreshold_DeductsLunch()
        {
            // Arrange: Lunch threshold at 13 (13:00), employee checks out at 13:00
            var options = new WageCalculationOptions
            {
                UseLunchEndThreshold = true,
                LunchEndHour = 13,
                LunchStartHour = 12
            };
            var service = new WageCalculationService(options);

            var emp = new Employee
            {
                ShiftStartTime = new TimeSpan(7, 0, 0),
                ShiftEndTime = new TimeSpan(17, 0, 0)
            };

            var date = new DateTime(2026, 7, 27); // Monday
            var record = new AttendanceRecord
            {
                Date = date,
                CheckInTime = date.AddHours(7),
                CheckOutTime = date.AddHours(13), // 13:00
                Status = AttendanceStatus.Present
            };

            // Act
            var breakdown = service.CalculateHours(record, emp);

            // Assert: 1.0 hour lunch deducted because checkout >= 13:00
            Assert.Equal(1.0, breakdown.Lunch);
            Assert.Equal(5.0, breakdown.Normal);
        }

        [Theory]
        [InlineData(false, 0.0)]
        [InlineData(true, 1.0)]
        public void SaturdayLunchDeductionSetting_RespectsConfiguredToggle(bool deductLunch, double expectedLunch)
        {
            // Arrange
            var options = new WageCalculationOptions
            {
                DeductLunchOnSaturday = deductLunch,
                LunchStartHour = 12,
                LunchEndHour = 13
            };
            var service = new WageCalculationService(options);
            var emp = new Employee();

            var date = new DateTime(2026, 8, 1); // Saturday
            var record = new AttendanceRecord
            {
                Date = date,
                CheckInTime = date.AddHours(7),
                CheckOutTime = date.AddHours(16),
                Status = AttendanceStatus.Present
            };

            // Act
            var breakdown = service.CalculateHours(record, emp);

            // Assert
            Assert.Equal(expectedLunch, breakdown.Lunch);
            Assert.Equal(9.0 - expectedLunch, breakdown.Overtime15);
        }

        [Theory]
        [InlineData(false, 0.0)]
        [InlineData(true, 1.0)]
        public void SundayLunchDeductionSetting_RespectsConfiguredToggle(bool deductLunch, double expectedLunch)
        {
            // Arrange
            var options = new WageCalculationOptions
            {
                DeductLunchOnSunday = deductLunch,
                LunchStartHour = 12,
                LunchEndHour = 13
            };
            var service = new WageCalculationService(options);
            var emp = new Employee();

            var date = new DateTime(2026, 8, 2); // Sunday
            var record = new AttendanceRecord
            {
                Date = date,
                CheckInTime = date.AddHours(7),
                CheckOutTime = date.AddHours(16),
                Status = AttendanceStatus.Present
            };

            // Act
            var breakdown = service.CalculateHours(record, emp);

            // Assert
            Assert.Equal(expectedLunch, breakdown.Lunch);
            Assert.Equal(9.0 - expectedLunch, breakdown.Overtime20);
        }

        [Theory]
        [InlineData(false, 0.0)]
        [InlineData(true, 1.0)]
        public void PublicHolidayLunchDeductionSetting_RespectsConfiguredToggle(bool deductLunch, double expectedLunch)
        {
            // Arrange
            var options = new WageCalculationOptions
            {
                DeductLunchOnPublicHoliday = deductLunch,
                LunchStartHour = 12,
                LunchEndHour = 13
            };
            var service = new WageCalculationService(options);
            var emp = new Employee();

            var date = new DateTime(2026, 8, 10); // National Women's Day (Observed - Monday)
            var record = new AttendanceRecord
            {
                Date = date,
                CheckInTime = date.AddHours(7),
                CheckOutTime = date.AddHours(16),
                Status = AttendanceStatus.Present
            };

            // Act
            var breakdown = service.CalculateHours(record, emp);

            // Assert
            Assert.Equal(expectedLunch, breakdown.Lunch);
            Assert.Equal(9.0 - expectedLunch, breakdown.Overtime20);
        }
    }
}
