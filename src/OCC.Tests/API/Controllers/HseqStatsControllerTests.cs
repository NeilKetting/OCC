using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.Shared.Enums;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class HseqStatsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<HseqStatsController>> _mockLogger;

        public HseqStatsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<HseqStatsController>>();
        }

        [Fact]
        public async Task GetDashboardStats_ReturnsCompanyWideDashboardMetrics()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqStatsController(context, _mockLogger.Object);

            var severeIncidentDate = DateTime.UtcNow.AddDays(-10);
            context.Incidents.AddRange(
                new Incident { Id = Guid.NewGuid(), Severity = IncidentSeverity.Critical, Date = severeIncidentDate, Type = IncidentType.Injury },
                new Incident { Id = Guid.NewGuid(), Severity = IncidentSeverity.Low, Date = DateTime.UtcNow.AddDays(-2), Type = IncidentType.NearMiss },
                new Incident { Id = Guid.NewGuid(), Severity = IncidentSeverity.Medium, Date = DateTime.UtcNow.AddDays(-1), Type = IncidentType.Environmental }
            );

            context.AttendanceRecords.AddRange(
                new AttendanceRecord { Id = Guid.NewGuid(), Date = DateTime.UtcNow.AddDays(-5), CheckInTime = DateTime.UtcNow.AddDays(-5).AddHours(8), CheckOutTime = DateTime.UtcNow.AddDays(-5).AddHours(16), HoursWorked = 8 },
                new AttendanceRecord { Id = Guid.NewGuid(), Date = DateTime.UtcNow.AddDays(-15), CheckInTime = DateTime.UtcNow.AddDays(-15).AddHours(8), CheckOutTime = DateTime.UtcNow.AddDays(-15).AddHours(16), HoursWorked = 8 }
            );

            context.HseqAudits.Add(new HseqAudit { Id = Guid.NewGuid(), SiteName = "Site A", ActualScore = 95, Date = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var result = await controller.GetDashboardStats();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetProjectSafeHours_WithEmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqStatsController(context, _mockLogger.Object);

            var result = await controller.GetProjectSafeHours(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetProjectSafeHours_CalculatesSafeHoursForProject()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqStatsController(context, _mockLogger.Object);

            var projectId = Guid.NewGuid();
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Status = AttendanceStatus.Present,
                Date = DateTime.UtcNow.AddDays(-1),
                CheckInTime = DateTime.UtcNow.Date.AddHours(8),
                CheckOutTime = DateTime.UtcNow.Date.AddHours(17),
                HoursWorked = 9
            });
            await context.SaveChangesAsync();

            var result = await controller.GetProjectSafeHours(projectId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var hours = Assert.IsType<double>(okResult.Value);
            Assert.Equal(9.0, hours);
        }

        [Fact]
        public async Task GetPerformanceHistory_WithInvalidYear_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqStatsController(context, _mockLogger.Object);

            var result = await controller.GetPerformanceHistory(1990);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetPerformanceHistory_WithValidYear_ReturnsMonthlyStats()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqStatsController(context, _mockLogger.Object);

            var targetYear = 2025;
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                Date = new DateTime(targetYear, 5, 10),
                HoursWorked = 8
            });
            context.Incidents.Add(new Incident
            {
                Id = Guid.NewGuid(),
                Date = new DateTime(targetYear, 5, 12),
                Type = IncidentType.NearMiss
            });
            await context.SaveChangesAsync();

            var result = await controller.GetPerformanceHistory(targetYear);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<List<HseqSafeHourRecord>>(okResult.Value);
            Assert.Equal(12, list.Count);
        }

        [Fact]
        public async Task RecalculateHours_UpdatesAttendanceRecords()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqStatsController(context, _mockLogger.Object);

            var mondayDate = new DateTime(2026, 3, 2); // Monday (Non-weekend, non-holiday)
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                Date = mondayDate,
                CheckInTime = mondayDate.AddHours(8),
                CheckOutTime = mondayDate.AddHours(17),
                HoursWorked = 0 // Initial incorrect hours
            });
            await context.SaveChangesAsync();

            var result = await controller.RecalculateHours();

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            var updatedRec = await context.AttendanceRecords.FirstAsync();
            Assert.Equal(8.0, updatedRec.HoursWorked); // 9 total hours - 1 lunch hour = 8 hours
        }

        [Fact]
        public async Task GetProjectDashboardStats_WithEmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqStatsController(context, _mockLogger.Object);

            var result = await controller.GetProjectDashboardStats(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetProjectDashboardStats_ReturnsComprehensiveStats()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqStatsController(context, _mockLogger.Object);

            var projId = Guid.NewGuid();
            var audit = new HseqAudit
            {
                Id = Guid.NewGuid(),
                ProjectId = projId,
                ActualScore = 90,
                Date = DateTime.UtcNow
            };
            audit.Sections.Add(new HseqAuditSection { Name = "PPE Compliance", PossibleScore = 100, ActualScore = 90 });

            context.HseqAudits.Add(audit);
            context.Incidents.Add(new Incident { Id = Guid.NewGuid(), ProjectId = projId, Type = IncidentType.NearMiss });
            await context.SaveChangesAsync();

            var result = await controller.GetProjectDashboardStats(projId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.NotNull(okResult.Value);
        }
    }
}
