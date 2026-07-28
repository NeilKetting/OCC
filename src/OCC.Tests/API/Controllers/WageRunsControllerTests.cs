using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Services;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class WageRunsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<IWageRunService> _mockWageRunService;
        private readonly Mock<ILogger<WageRunsController>> _mockLogger;

        public WageRunsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockWageRunService = new Mock<IWageRunService>();
            _mockLogger = new Mock<ILogger<WageRunsController>>();
        }

        [Fact]
        public async Task GetWageRuns_ReturnsAllRuns()
        {
            using var context = new AppDbContext(_dbOptions);
            context.WageRuns.AddRange(
                new WageRun { Id = Guid.NewGuid(), StartDate = DateTime.Today.AddDays(-14), EndDate = DateTime.Today.AddDays(-1), Status = WageRunStatus.Finalized },
                new WageRun { Id = Guid.NewGuid(), StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(13), Status = WageRunStatus.Draft }
            );
            await context.SaveChangesAsync();

            var controller = new WageRunsController(context, _mockWageRunService.Object, _mockLogger.Object);

            var result = await controller.GetWageRuns();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var runs = Assert.IsAssignableFrom<IEnumerable<WageRun>>(okResult.Value).ToList();
            Assert.Equal(2, runs.Count);
        }

        [Fact]
        public async Task GetWageRun_ValidId_ReturnsRun()
        {
            using var context = new AppDbContext(_dbOptions);
            var runId = Guid.NewGuid();
            context.WageRuns.Add(new WageRun
            {
                Id = runId,
                StartDate = DateTime.Today.AddDays(-7),
                EndDate = DateTime.Today,
                Status = WageRunStatus.Draft
            });
            await context.SaveChangesAsync();

            var controller = new WageRunsController(context, _mockWageRunService.Object, _mockLogger.Object);

            var result = await controller.GetWageRun(runId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var run = Assert.IsType<WageRun>(okResult.Value);
            Assert.Equal(runId, run.Id);
        }

        [Fact]
        public async Task GenerateDraft_ValidRequest_CallsServiceAndReturnsOk()
        {
            using var context = new AppDbContext(_dbOptions);
            var requestRun = new WageRun { StartDate = DateTime.Today.AddDays(-14), EndDate = DateTime.Today.AddDays(-1), Branch = "JHB" };
            var draftResult = new WageRun { Id = Guid.NewGuid(), Status = WageRunStatus.Draft };

            _mockWageRunService.Setup(s => s.GenerateDraftAsync(It.IsAny<WageRun>())).ReturnsAsync(draftResult);

            var controller = new WageRunsController(context, _mockWageRunService.Object, _mockLogger.Object);

            var result = await controller.GenerateDraft(requestRun);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var run = Assert.IsType<WageRun>(okResult.Value);
            Assert.Equal(draftResult.Id, run.Id);
        }

        [Fact]
        public async Task GenerateDraft_InvalidDates_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new WageRunsController(context, _mockWageRunService.Object, _mockLogger.Object);

            var invalidRequest = new WageRun { StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(-5) };

            var result = await controller.GenerateDraft(invalidRequest);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task FinalizeRun_ValidRun_ReturnsCreatedResult()
        {
            using var context = new AppDbContext(_dbOptions);
            var runId = Guid.NewGuid();
            var run = new WageRun { Id = runId, StartDate = DateTime.Today.AddDays(-14), EndDate = DateTime.Today.AddDays(-1), Status = WageRunStatus.Draft };
            var finalizedRun = new WageRun { Id = runId, Status = WageRunStatus.Finalized };

            _mockWageRunService.Setup(s => s.FinalizeRunAsync(It.IsAny<WageRun>())).ReturnsAsync(finalizedRun);

            var controller = new WageRunsController(context, _mockWageRunService.Object, _mockLogger.Object);

            var result = await controller.FinalizeRun(run);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var res = Assert.IsType<WageRun>(createdResult.Value);
            Assert.Equal(WageRunStatus.Finalized, res.Status);
        }

        [Fact]
        public async Task UpdateDraftLines_DraftRun_UpdatesWashingAndSupervisorIncentives()
        {
            using var context = new AppDbContext(_dbOptions);
            var runId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            var run = new WageRun
            {
                Id = runId,
                Status = WageRunStatus.Draft,
                Lines = new List<WageRunLine>
                {
                    new WageRunLine { Id = lineId, DeductionWashing = 0, IncentiveSupervisor = 0 }
                }
            };
            context.WageRuns.Add(run);
            await context.SaveChangesAsync();

            var controller = new WageRunsController(context, _mockWageRunService.Object, _mockLogger.Object);

            var updateLines = new List<WageRunLine>
            {
                new WageRunLine { Id = lineId, DeductionWashing = 150.0m, IncentiveSupervisor = 500.0m }
            };

            var result = await controller.UpdateDraftLines(runId, updateLines);

            Assert.IsType<NoContentResult>(result);

            var lineInDb = await context.WageRunLines.FindAsync(lineId);
            Assert.Equal(150.0m, lineInDb!.DeductionWashing);
            Assert.Equal(500.0m, lineInDb.IncentiveSupervisor);
        }

        [Fact]
        public async Task GetBankExportData_ValidRun_ReturnsPaymentDtos()
        {
            using var context = new AppDbContext(_dbOptions);
            var runId = Guid.NewGuid();
            var empId = Guid.NewGuid();

            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "Bob",
                LastName = "Bank",
                BankName = "FNB",
                AccountNumber = "123456789",
                BranchCode = "250655",
                IsActive = true
            });

            var run = new WageRun
            {
                Id = runId,
                EndDate = new DateTime(2026, 7, 28),
                Lines = new List<WageRunLine>
                {
                    new WageRunLine
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = empId,
                        EmployeeName = "Bob Bank",
                        EmployeeNumber = "EMP005",
                        HourlyRate = 100,
                        NormalHours = 40,
                        TotalWage = 4000.0m
                    }
                }
            };
            context.WageRuns.Add(run);
            await context.SaveChangesAsync();

            var controller = new WageRunsController(context, _mockWageRunService.Object, _mockLogger.Object);

            var result = await controller.GetBankExportData(runId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payments = Assert.IsAssignableFrom<IEnumerable<BankPaymentDto>>(okResult.Value).ToList();
            Assert.Single(payments);
            Assert.Equal("Bob Bank", payments[0].EmployeeName);
            Assert.Equal(4000.0m, payments[0].Amount);
            Assert.Equal("Wage 20260728", payments[0].Reference);
        }

        [Fact]
        public async Task DeleteRun_DraftRun_DeletesSuccessfully()
        {
            using var context = new AppDbContext(_dbOptions);
            var runId = Guid.NewGuid();
            context.WageRuns.Add(new WageRun { Id = runId, Status = WageRunStatus.Draft });
            await context.SaveChangesAsync();

            var controller = new WageRunsController(context, _mockWageRunService.Object, _mockLogger.Object);

            var result = await controller.DeleteRun(runId);

            Assert.IsType<NoContentResult>(result);

            var runInDb = await context.WageRuns.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == runId);
            Assert.True(runInDb == null || !runInDb.IsActive);
        }

        [Fact]
        public async Task DeleteRun_FinalizedRun_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var runId = Guid.NewGuid();
            context.WageRuns.Add(new WageRun { Id = runId, Status = WageRunStatus.Finalized });
            await context.SaveChangesAsync();

            var controller = new WageRunsController(context, _mockWageRunService.Object, _mockLogger.Object);

            var result = await controller.DeleteRun(runId);

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
