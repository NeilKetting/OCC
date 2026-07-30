using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using OCC.API.Data;
using OCC.API.Services;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Features.WagesHub
{
    public class WageRunServiceOverhaulTests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private Mock<IWageCalculationService> GetMockWageCalculationService()
        {
            var mock = new Mock<IWageCalculationService>();
            mock.Setup(m => m.CalculateHours(It.IsAny<AttendanceRecord>(), It.IsAny<Employee>()))
                .Returns((AttendanceRecord rec, Employee emp) =>
                {
                    if (rec.Status == AttendanceStatus.Present)
                        return new HoursBreakdown(9.0, 0, 0, 1.0);
                    return new HoursBreakdown(0, 0, 0, 0);
                });
            return mock;
        }

        [Fact]
        public async Task FinalizeRunAsync_TagsAllAttendanceRecordsInPeriodIncludingUnpaidLeaveAndAbsent()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockWageCalc = GetMockWageCalculationService();
            var mockConfig = new Mock<IConfiguration>();
            var service = new WageRunService(context, mockWageCalc.Object, mockConfig.Object);

            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "Test",
                LastName = "Worker",
                EmployeeNumber = "EMP001",
                Branch = "Cape Town",
                RateType = RateType.Hourly,
                HourlyRate = 100
            });

            var startDate = new DateTime(2026, 7, 1);
            var endDate = new DateTime(2026, 7, 7);

            // Add attendance records: 1 worked, 1 unpaid leave, 1 absent
            var workedRecord = new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = empId, Date = startDate, Status = AttendanceStatus.Present };
            var unpaidLeaveRecord = new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = empId, Date = startDate.AddDays(1), Status = AttendanceStatus.UnpaidLeave };
            var absentRecord = new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = empId, Date = startDate.AddDays(2), Status = AttendanceStatus.Absent };

            context.AttendanceRecords.AddRange(workedRecord, unpaidLeaveRecord, absentRecord);
            await context.SaveChangesAsync();

            var run = new WageRun
            {
                Id = Guid.NewGuid(),
                StartDate = startDate,
                EndDate = endDate,
                Branch = "Cape Town",
                PayType = "Hourly",
                PayFrequency = PayFrequency.Weekly,
                RunType = WageRunType.Standard,
                Lines = new List<WageRunLine>
                {
                    new WageRunLine { Id = Guid.NewGuid(), EmployeeId = empId, EmployeeName = "Test Worker", HourlyRate = 100 }
                }
            };

            // Act
            await service.FinalizeRunAsync(run);

            // Assert: ALL records including UnpaidLeave and Absent must be tagged with PaidWageRunId
            var updatedWorked = await context.AttendanceRecords.FindAsync(workedRecord.Id);
            var updatedUnpaid = await context.AttendanceRecords.FindAsync(unpaidLeaveRecord.Id);
            var updatedAbsent = await context.AttendanceRecords.FindAsync(absentRecord.Id);

            Assert.NotNull(updatedWorked?.PaidWageRunId);
            Assert.NotNull(updatedUnpaid?.PaidWageRunId);
            Assert.NotNull(updatedAbsent?.PaidWageRunId);
        }

        [Fact]
        public async Task GenerateDraftAsync_WithPriorAdHocRun_AutoRecoversAdvanceDeduction()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockWageCalc = GetMockWageCalculationService();
            var mockConfig = new Mock<IConfiguration>();

            context.WageSettings.Add(new WageSettings
            {
                Id = Guid.NewGuid(),
                AutoRecoverAdHocAdvances = true
            });

            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "Mamparra",
                LastName = "Employee",
                EmployeeNumber = "EMP002",
                Branch = "Johannesburg",
                Status = EmployeeStatus.Active,
                RateType = RateType.Hourly,
                HourlyRate = 100
            });

            // Add a prior finalized AdHocAdvance run with R500 net pay
            var adHocRunId = Guid.NewGuid();
            var priorAdHocRun = new WageRun
            {
                Id = adHocRunId,
                StartDate = new DateTime(2026, 7, 1),
                EndDate = new DateTime(2026, 7, 3),
                Branch = "Johannesburg",
                PayType = "Hourly",
                RunType = WageRunType.AdHocAdvance,
                Status = WageRunStatus.Finalized,
                Lines = new List<WageRunLine>
                {
                    new WageRunLine
                    {
                        Id = Guid.NewGuid(),
                        WageRunId = adHocRunId,
                        EmployeeId = empId,
                        TotalWage = 500m
                    }
                }
            };
            context.WageRuns.Add(priorAdHocRun);
            await context.SaveChangesAsync();

            var service = new WageRunService(context, mockWageCalc.Object, mockConfig.Object);

            var request = new WageRun
            {
                StartDate = new DateTime(2026, 7, 6),
                EndDate = new DateTime(2026, 7, 19),
                Branch = "Johannesburg",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Fortnightly
            };

            // Act
            var draft = await service.GenerateDraftAsync(request);

            // Assert
            var line = draft.Lines.FirstOrDefault(l => l.EmployeeId == empId);
            Assert.NotNull(line);
            Assert.Equal(500m, line.DeductionAdvanceRecovery);
            Assert.Contains("Adv Recovery", line.Comments);
        }
    }
}
