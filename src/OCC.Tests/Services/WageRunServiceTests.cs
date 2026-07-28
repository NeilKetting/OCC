using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Data;
using OCC.API.Services;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Services
{
    public class WageRunServiceTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<IWageCalculationService> _mockWageCalc;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<ILogger<WageRunService>> _mockLogger;

        public WageRunServiceTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockWageCalc = new Mock<IWageCalculationService>();
            _mockConfig = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<WageRunService>>();

            _mockWageCalc.Setup(w => w.CalculateHours(It.IsAny<AttendanceRecord>(), It.IsAny<Employee>()))
                .Returns(new HoursBreakdown(8.0, 0.0, 0.0, 1.0));
        }

        [Fact]
        public async Task GenerateDraftAsync_ExistingFinalizedRun_ThrowsArgumentException()
        {
            using var context = new AppDbContext(_dbOptions);
            var startDate = new DateTime(2026, 7, 1);
            var endDate = new DateTime(2026, 7, 14);

            context.WageRuns.Add(new WageRun
            {
                Id = Guid.NewGuid(),
                StartDate = startDate,
                EndDate = endDate,
                Branch = "JHB",
                PayType = "Hourly",
                Status = WageRunStatus.Finalized
            });
            await context.SaveChangesAsync();

            var service = new WageRunService(context, _mockWageCalc.Object, _mockConfig.Object, _mockLogger.Object);

            var request = new WageRun
            {
                StartDate = startDate,
                EndDate = endDate,
                Branch = "JHB",
                PayType = "Hourly"
            };

            await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateDraftAsync(request));
        }

        [Fact]
        public async Task GenerateDraftAsync_ValidActiveEmployees_GeneratesDraftRunLines()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "David",
                LastName = "Miller",
                Branch = "JHB",
                Status = EmployeeStatus.Active,
                RateType = RateType.Hourly,
                HourlyRate = 120.0,
                LivesInCompanyHousing = true
            });

            var startDate = new DateTime(2026, 7, 1);
            var endDate = new DateTime(2026, 7, 14);

            context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                Date = startDate.AddDays(1),
                CheckInTime = startDate.AddDays(1).AddHours(7),
                CheckOutTime = startDate.AddDays(1).AddHours(16),
                Status = AttendanceStatus.Present
            });

            await context.SaveChangesAsync();

            var service = new WageRunService(context, _mockWageCalc.Object, _mockConfig.Object, _mockLogger.Object);

            var request = new WageRun
            {
                StartDate = startDate,
                EndDate = endDate,
                Branch = "JHB",
                PayType = "Hourly",
                InputTotalGasCharge = 200.0m,
                InputCompanyHousingWashingFee = 50.0m
            };

            var draft = await service.GenerateDraftAsync(request);

            Assert.NotNull(draft);
            Assert.Equal(WageRunStatus.Draft, draft.Status);
            Assert.Single(draft.Lines);

            var line = draft.Lines.First();
            Assert.Equal(empId, line.EmployeeId);
            Assert.Equal(120.0m, line.HourlyRate);
            Assert.Equal(200.0m, line.DeductionGas); // 1 housed employee gets full gas charge split
            Assert.Equal(50.0m, line.DeductionWashing);
        }

        [Fact]
        public async Task FinalizeRunAsync_DeductsLoanBalanceAndMarksAttendancePaid()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();

            var loan = new EmployeeLoan
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                PrincipalAmount = 1000m,
                MonthlyInstallment = 200m,
                OutstandingBalance = 200m,
                IsActive = true,
                StartDate = DateTime.Today.AddMonths(-1)
            };
            context.EmployeeLoans.Add(loan);

            var attRecord = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                Date = new DateTime(2026, 7, 5),
                Status = AttendanceStatus.Present
            };
            context.AttendanceRecords.Add(attRecord);
            await context.SaveChangesAsync();

            var service = new WageRunService(context, _mockWageCalc.Object, _mockConfig.Object, _mockLogger.Object);

            var run = new WageRun
            {
                Id = Guid.NewGuid(),
                StartDate = new DateTime(2026, 7, 1),
                EndDate = new DateTime(2026, 7, 14),
                Branch = "JHB",
                PayType = "Hourly",
                Lines = new List<WageRunLine>
                {
                    new WageRunLine
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = empId,
                        EmployeeName = "David Miller",
                        DeductionLoan = 200m
                    }
                }
            };

            var finalized = await service.FinalizeRunAsync(run);

            Assert.Equal(WageRunStatus.Finalized, finalized.Status);

            var loanInDb = await context.EmployeeLoans.FindAsync(loan.Id);
            Assert.Equal(0m, loanInDb!.OutstandingBalance);
            Assert.False(loanInDb.IsActive);

            var attInDb = await context.AttendanceRecords.FindAsync(attRecord.Id);
            Assert.Equal(finalized.Id, attInDb!.PaidWageRunId);
        }
    }
}
