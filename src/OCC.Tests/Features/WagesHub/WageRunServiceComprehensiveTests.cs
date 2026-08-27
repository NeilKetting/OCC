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
    /// <summary>
    /// Comprehensive test suite covering every wage run outcome, pay frequency, run type, and settings override.
    /// </summary>
    public class WageRunServiceComprehensiveTests
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
            mock.Setup(m => m.CalculateHours(It.IsAny<AttendanceRecord>(), It.IsAny<Employee>(), It.IsAny<WageSettings?>()))
                .Returns((AttendanceRecord rec, Employee emp, WageSettings? settings) =>
                {
                    if (rec.Status == AttendanceStatus.Present)
                        return new HoursBreakdown(9.0, 0, 0, 1.0);
                    return new HoursBreakdown(0, 0, 0, 0);
                });
            return mock;
        }

        [Fact]
        public async Task GenerateDraftAsync_UsesCustomBibcRateAndHousingFeesFromWageSettings()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockWageCalc = GetMockWageCalculationService();
            var mockConfig = new Mock<IConfiguration>();

            context.WageSettings.Add(new WageSettings
            {
                Id = Guid.NewGuid(),
                BibcRatePerDay = 35.50m,
                DefaultSupervisorFee = 750.00m,
                DefaultCompanyHousingWashingFee = 120.00m
            });

            var supervisorId = Guid.NewGuid();
            var housedId = Guid.NewGuid();

            context.Employees.AddRange(
                new Employee
                {
                    Id = supervisorId,
                    FirstName = "Sup",
                    LastName = "Visor",
                    EmployeeNumber = "SUP01",
                    Branch = "Cape Town",
                    Role = EmployeeRole.Supervisor,
                    Status = EmployeeStatus.Active,
                    RateType = RateType.Hourly,
                    HourlyRate = 120,
                    IsBibc = true
                },
                new Employee
                {
                    Id = housedId,
                    FirstName = "Housed",
                    LastName = "Worker",
                    EmployeeNumber = "HOU01",
                    Branch = "Cape Town",
                    Status = EmployeeStatus.Active,
                    LivesInCompanyHousing = true,
                    RateType = RateType.Hourly,
                    HourlyRate = 100,
                    IsBibc = true
                }
            );

            var startDate = new DateTime(2026, 7, 6);
            var endDate = new DateTime(2026, 7, 12);

            // Add worked attendance records for Cape Town (5 days)
            for (int i = 0; i < 5; i++)
            {
                var d = startDate.AddDays(i);
                context.AttendanceRecords.Add(new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = supervisorId, Date = d, Status = AttendanceStatus.Present });
                context.AttendanceRecords.Add(new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = housedId, Date = d, Status = AttendanceStatus.Present });
            }
            await context.SaveChangesAsync();

            var service = new WageRunService(context, mockWageCalc.Object, mockConfig.Object);

            var request = new WageRun
            {
                StartDate = startDate,
                EndDate = endDate,
                Branch = "Cape Town",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Weekly,
                InputTotalGasCharge = 300m
            };

            // Act
            var draft = await service.GenerateDraftAsync(request);

            // Assert
            var supLine = draft.Lines.First(l => l.EmployeeId == supervisorId);
            var housedLine = draft.Lines.First(l => l.EmployeeId == housedId);

            // Supervisor Fee must use configured 750.00m
            Assert.Equal(750.00m, supLine.IncentiveSupervisor);

            // Housing Washing Fee must use configured 120.00m
            Assert.Equal(120.00m, housedLine.DeductionWashing);
            Assert.Equal(300.00m, housedLine.DeductionGas); // 1 housed employee = full gas charge

            // BIBC Amount must use configured 35.50m per day worked
            Assert.True(supLine.TotalDaysWorked > 0);
            Assert.Equal(35.50m * (decimal)supLine.TotalDaysWorked, supLine.BibcAmount);
        }

        [Fact]
        public async Task GenerateDraftAsync_WhenProjectedHoursDisabledInSettings_SetsProjectedHoursToZero()
        {
            // Arrange: Disable projected hours in WageSettings
            using var context = GetInMemoryDbContext();
            var mockWageCalc = GetMockWageCalculationService();
            var mockConfig = new Mock<IConfiguration>();

            context.WageSettings.Add(new WageSettings
            {
                Id = Guid.NewGuid(),
                EnableProjectedHours = false
            });

            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "NoProj",
                LastName = "Worker",
                EmployeeNumber = "NOPROJ1",
                Branch = "Johannesburg",
                Status = EmployeeStatus.Active,
                RateType = RateType.Hourly,
                HourlyRate = 100
            });
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

            // Assert: Projected hours must be 0 because settings disabled it
            var line = draft.Lines.First(l => l.EmployeeId == empId);
            Assert.Equal(0.0, line.ProjectedHours);
        }

        [Fact]
        public async Task FullMamparraAdvanceAndRecoveryWorkflow_CalculatesNetPayAndAdvanceRecoveryAccurately()
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
                FirstName = "John",
                LastName = "Doe",
                EmployeeNumber = "EMP100",
                Branch = "Johannesburg",
                Status = EmployeeStatus.Active,
                RateType = RateType.Hourly,
                HourlyRate = 100
            });

            // 1. Run AdHoc "Mamparra" Run mid-week
            var service = new WageRunService(context, mockWageCalc.Object, mockConfig.Object);

            var mamparraRun = new WageRun
            {
                Id = Guid.NewGuid(),
                StartDate = new DateTime(2026, 7, 20),
                EndDate = new DateTime(2026, 7, 26),
                Branch = "Johannesburg",
                PayType = "Hourly",
                RunType = WageRunType.AdHocAdvance,
                PayFrequency = PayFrequency.Weekly,
                Lines = new List<WageRunLine>
                {
                    new WageRunLine
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = empId,
                        EmployeeName = "John Doe",
                        HourlyRate = 100,
                        NormalHours = 27, // Mon-Wed
                        ProjectedHours = 18, // Thu-Fri projected
                        TotalWage = 4500m
                    }
                }
            };

            await service.FinalizeRunAsync(mamparraRun);

            // 2. Next Regular Run generated
            var nextRunRequest = new WageRun
            {
                StartDate = new DateTime(2026, 7, 27),
                EndDate = new DateTime(2026, 8, 9),
                Branch = "Johannesburg",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Fortnightly
            };

            // Act
            var draftNextRun = await service.GenerateDraftAsync(nextRunRequest);

            // Assert
            var line = draftNextRun.Lines.First(l => l.EmployeeId == empId);
            Assert.Equal(4500m, line.DeductionAdvanceRecovery);
            Assert.True(line.NetPay >= 0m);
        }

        [Fact]
        public async Task ConsecutiveThreeWageRuns_PreventsUnpaidLeaveDoubleDeduction()
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
                FirstName = "Unpaid",
                LastName = "LeaveTest",
                EmployeeNumber = "UL001",
                Branch = "Cape Town",
                Status = EmployeeStatus.Active,
                RateType = RateType.Hourly,
                HourlyRate = 100
            });

            // Unpaid leave on July 2nd
            var unpaidRecord = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                Date = new DateTime(2026, 7, 2),
                Status = AttendanceStatus.UnpaidLeave
            };
            context.AttendanceRecords.Add(unpaidRecord);
            await context.SaveChangesAsync();

            // Run 1: July 1 - July 7
            var run1 = new WageRun
            {
                Id = Guid.NewGuid(),
                StartDate = new DateTime(2026, 7, 1),
                EndDate = new DateTime(2026, 7, 7),
                Branch = "Cape Town",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Weekly,
                Lines = new List<WageRunLine> { new WageRunLine { Id = Guid.NewGuid(), EmployeeId = empId } }
            };
            await service.FinalizeRunAsync(run1);

            // Run 2 Draft: July 8 - July 14
            var run2Request = new WageRun
            {
                StartDate = new DateTime(2026, 7, 8),
                EndDate = new DateTime(2026, 7, 14),
                Branch = "Cape Town",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Weekly
            };
            var draft2 = await service.GenerateDraftAsync(run2Request);
            var line2 = draft2.Lines.First(l => l.EmployeeId == empId);

            // Run 3 Draft: July 15 - July 21
            var run3Request = new WageRun
            {
                StartDate = new DateTime(2026, 7, 15),
                EndDate = new DateTime(2026, 7, 21),
                Branch = "Cape Town",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Weekly
            };
            var draft3 = await service.GenerateDraftAsync(run3Request);
            var line3 = draft3.Lines.First(l => l.EmployeeId == empId);

            // Assert: July 2nd unpaid leave must NOT cause negative variance in Run 2 or Run 3!
            Assert.DoesNotContain("02/07", line2.VarianceNotes);
            Assert.DoesNotContain("02/07", line3.VarianceNotes);
        }

        [Fact]
        public async Task GenerateDraftAsync_AbsenceOnProjectedThursdayAndFriday_DeductsVarianceAndDisplaysNotesCorrectlyAcrossFrequencySwitch()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockWageCalc = GetMockWageCalculationService();
            var mockConfig = new Mock<IConfiguration>();

            context.WageSettings.Add(new WageSettings
            {
                Id = Guid.NewGuid(),
                EnableProjectedHours = true
            });

            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "Jhb",
                LastName = "Worker",
                EmployeeNumber = "JHB01",
                Branch = "Johannesburg",
                Status = EmployeeStatus.Active,
                RateType = RateType.Hourly,
                HourlyRate = 100,
                ShiftStartTime = new TimeSpan(7, 0, 0),
                ShiftEndTime = new TimeSpan(17, 0, 0) // 9 hours work day
            });

            // Attendance: Worker was Absent on Thu July 9 & Fri July 10 (the 2 projected days of prior run)
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                Date = new DateTime(2026, 7, 9), // Thursday
                Status = AttendanceStatus.Absent
            });
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                Date = new DateTime(2026, 7, 10), // Friday
                Status = AttendanceStatus.Absent
            });
            await context.SaveChangesAsync();

            var service = new WageRunService(context, mockWageCalc.Object, mockConfig.Object);

            // Prior run (Fortnightly): End date July 10, Start date June 27
            var lastRun = new WageRun
            {
                Id = Guid.NewGuid(),
                StartDate = new DateTime(2026, 6, 27),
                EndDate = new DateTime(2026, 7, 10),
                Branch = "Johannesburg",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Fortnightly,
                Lines = new List<WageRunLine>
                {
                    new WageRunLine { Id = Guid.NewGuid(), EmployeeId = empId, ProjectedHours = 18.0 }
                }
            };
            await service.FinalizeRunAsync(lastRun);

            // Current run (Switched to Weekly): Start date July 11
            var weeklyRequest = new WageRun
            {
                StartDate = new DateTime(2026, 7, 11),
                EndDate = new DateTime(2026, 7, 17),
                Branch = "Johannesburg",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Weekly
            };

            // Act
            var draft = await service.GenerateDraftAsync(weeklyRequest);

            // Assert
            var line = draft.Lines.First(l => l.EmployeeId == empId);
            Assert.Equal(-18.0, line.VarianceHours); // Deducts 18 hours (2 absent days)
            Assert.Contains("Adv Adj", line.VarianceNotes);
            Assert.Contains("Absent -2 day(s)", line.VarianceNotes);
        }

        [Fact]
        public async Task GenerateDraftAsync_PublicHoliday_AbsentEmployee_CountsInWeek1AndCalculatesFullBibc()
        {
            // Arrange: 08 Aug 2026 to 14 Aug 2026 (Public Holiday on Mon 10 Aug 2026)
            using var context = GetInMemoryDbContext();
            var realWageCalc = new WageCalculationService(new WageCalculationOptions());
            var mockConfig = new Mock<IConfiguration>();

            var empId = Guid.NewGuid();
            var emp = new Employee
            {
                Id = empId,
                FirstName = "Mphuthumi",
                LastName = "Dododo",
                Branch = "Cape Town",
                IsBibc = true,
                IsActive = true,
                RateType = RateType.Hourly,
                HourlyRate = 50.0
            };
            context.Employees.Add(emp);

            context.WageSettings.Add(new WageSettings
            {
                Id = Guid.NewGuid(),
                BibcRatePerDay = 28.75m
            });

            // 4 days present (Tue 11 Aug to Fri 14 Aug)
            for (var d = new DateTime(2026, 8, 11); d <= new DateTime(2026, 8, 14); d = d.AddDays(1))
            {
                context.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = empId,
                    Date = d,
                    Status = AttendanceStatus.Present,
                    CheckInTime = d.AddHours(7),
                    CheckOutTime = d.AddHours(16.5),
                    Branch = "Cape Town"
                });
            }

            // 1 day absent on Public Holiday (Mon 10 Aug 2026)
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                Date = new DateTime(2026, 8, 10),
                Status = AttendanceStatus.Absent,
                Branch = "Cape Town",
                Notes = "Did not work on P/Holiday"
            });

            await context.SaveChangesAsync();

            var service = new WageRunService(context, realWageCalc, mockConfig.Object);
            var request = new WageRun
            {
                StartDate = new DateTime(2026, 8, 8),
                EndDate = new DateTime(2026, 8, 14),
                Branch = "Cape Town",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Weekly
            };

            // Act
            var draft = await service.GenerateDraftAsync(request);

            // Assert
            var line = draft.Lines.First(l => l.EmployeeId == empId);
            Assert.Equal(5.0, line.DaysWorkedWeek2); // DaysWorkedWeek2 maps to WEEK 1 column in UI
            Assert.Equal(5.0, line.TotalDaysWorked); // TotalDaysWorked = 5
            Assert.Equal(143.75m, line.BibcAmount);  // 5 days x R28.75 = R143.75
        }

        [Fact]
        public async Task GenerateDraftAsync_RespectsLoanStartDate_DoesNotDeductFutureLoan()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockWageCalc = GetMockWageCalculationService();
            var mockConfig = new Mock<IConfiguration>();

            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "Future",
                LastName = "LoanUser",
                EmployeeNumber = "EMP999",
                Branch = "Cape Town",
                Status = EmployeeStatus.Active,
                RateType = RateType.Hourly,
                HourlyRate = 100
            });

            // Add a loan with StartDate set in the future relative to the pay period
            // Pay period: 2026-08-01 to 2026-08-14. Loan StartDate: 2026-08-28.
            context.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                PrincipalAmount = 1000m,
                MonthlyInstallment = 200m,
                OutstandingBalance = 1000m,
                StartDate = new DateTime(2026, 8, 28),
                IsActive = true,
                Notes = "[Term: Fortnightly, Installments: 5]"
            });

            await context.SaveChangesAsync();

            var service = new WageRunService(context, mockWageCalc.Object, mockConfig.Object);
            var request = new WageRun
            {
                StartDate = new DateTime(2026, 8, 1),
                EndDate = new DateTime(2026, 8, 14),
                Branch = "Cape Town",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Fortnightly
            };

            // Act
            var draft = await service.GenerateDraftAsync(request);

            // Assert
            var line = draft.Lines.First(l => l.EmployeeId == empId);
            Assert.Equal(0m, line.DeductionLoan); // Future loan must NOT be deducted
        }

        [Fact]
        public async Task GenerateDraftAsync_RespectsLoanStartDate_DeductsActiveLoanWhenStartDateReached()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockWageCalc = GetMockWageCalculationService();
            var mockConfig = new Mock<IConfiguration>();

            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "Started",
                LastName = "LoanUser",
                EmployeeNumber = "EMP888",
                Branch = "Cape Town",
                Status = EmployeeStatus.Active,
                RateType = RateType.Hourly,
                HourlyRate = 100
            });

            // Add a loan with StartDate set on or before the pay period end date
            // Pay period: 2026-08-15 to 2026-08-28. Loan StartDate: 2026-08-28.
            context.EmployeeLoans.Add(new EmployeeLoan
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                PrincipalAmount = 1000m,
                MonthlyInstallment = 200m,
                OutstandingBalance = 1000m,
                StartDate = new DateTime(2026, 8, 28),
                IsActive = true,
                Notes = "[Term: Fortnightly, Installments: 5]"
            });

            await context.SaveChangesAsync();

            var service = new WageRunService(context, mockWageCalc.Object, mockConfig.Object);
            var request = new WageRun
            {
                StartDate = new DateTime(2026, 8, 15),
                EndDate = new DateTime(2026, 8, 28),
                Branch = "Cape Town",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Fortnightly
            };

            // Act
            var draft = await service.GenerateDraftAsync(request);

            // Assert
            var line = draft.Lines.First(l => l.EmployeeId == empId);
            Assert.Equal(200m, line.DeductionLoan); // Started loan MUST be deducted
        }

        [Fact]
        public async Task GenerateDraftAsync_HalfDayLeave_FormatsNotesAndCommentsWithPeriodAndPaidState()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockWageCalc = new Mock<IWageCalculationService>();
            var mockConfig = new Mock<IConfiguration>();

            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "Nathan",
                LastName = "Stemmet",
                EmployeeNumber = "467",
                Branch = "Cape Town",
                Status = EmployeeStatus.Active,
                RateType = RateType.Hourly,
                HourlyRate = 100
            });

            var leaveDate = new DateTime(2026, 8, 28);

            context.LeaveRequests.Add(new LeaveRequest
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                StartDate = leaveDate,
                EndDate = leaveDate,
                DurationType = LeaveDurationType.MorningHalfDay,
                LeaveType = LeaveType.Annual,
                Status = LeaveStatus.Approved,
                PaidDays = 0.5,
                NumberOfDays = 0.5,
                IsUnpaid = false
            });

            mockWageCalc.Setup(m => m.CalculateHours(It.IsAny<AttendanceRecord>(), It.IsAny<Employee>(), It.IsAny<WageSettings?>()))
                .Returns(new HoursBreakdown(4.25, 0, 0, 0));

            await context.SaveChangesAsync();

            var service = new WageRunService(context, mockWageCalc.Object, mockConfig.Object);
            var request = new WageRun
            {
                StartDate = new DateTime(2026, 8, 15),
                EndDate = new DateTime(2026, 8, 28),
                Branch = "Cape Town",
                PayType = "Hourly",
                RunType = WageRunType.Standard,
                PayFrequency = PayFrequency.Fortnightly
            };

            // Act
            var draft = await service.GenerateDraftAsync(request);

            // Assert
            var line = draft.Lines.First(l => l.EmployeeId == empId);
            Assert.Contains("28/08: Paid Leave - Half Day (Morning);", line.VarianceNotes);
            Assert.Contains("Paid Leave - Half Day (Morning) (0.5d: 28/08)", line.Comments);
        }
    }
}
