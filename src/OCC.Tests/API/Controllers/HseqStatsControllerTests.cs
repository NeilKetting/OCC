using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.Shared.Models;
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
                .Options;

            _mockLogger = new Mock<ILogger<HseqStatsController>>();
        }

        [Fact]
        public async Task GetProjectDashboardStats_ReturnsAuditsMatchingProjectIdOrSiteName()
        {
            // Arrange
            using var context = new AppDbContext(_dbOptions);
            var projectId = Guid.NewGuid();
            var projectName = "Engen Blue Bay";

            var project = new Project
            {
                Id = projectId,
                Name = projectName,
                Status = "In Progress",
                CreatedAtUtc = DateTime.UtcNow
            };
            context.Projects.Add(project);

            // Audit 1 linked by ProjectId
            var audit1 = new HseqAudit
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                SiteName = projectName,
                ActualScore = 90,
                TargetScore = 100,
                Date = DateTime.Today.AddDays(-10),
                Sections = new List<HseqAuditSection>
                {
                    new HseqAuditSection { Id = Guid.NewGuid(), Name = "PPE", ActualScore = 9, PossibleScore = 10 }
                }
            };

            // Audit 2 linked by SiteName only (ProjectId null)
            var audit2 = new HseqAudit
            {
                Id = Guid.NewGuid(),
                ProjectId = null,
                SiteName = "Engen Blue Bay Site",
                ActualScore = 100,
                TargetScore = 100,
                Date = DateTime.Today.AddDays(-5),
                Sections = new List<HseqAuditSection>
                {
                    new HseqAuditSection { Id = Guid.NewGuid(), Name = "PPE", ActualScore = 10, PossibleScore = 10 }
                }
            };

            context.HseqAudits.AddRange(audit1, audit2);
            await context.SaveChangesAsync();

            var controller = new HseqStatsController(context, _mockLogger.Object);

            // Act
            var actionResult = await controller.GetProjectDashboardStats(projectId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var val = okResult.Value;
            Assert.NotNull(val);

            var auditsTotalProp = val.GetType().GetProperty("AuditsTotal");
            var averageScoreProp = val.GetType().GetProperty("AverageAuditScore");

            Assert.NotNull(auditsTotalProp);
            Assert.NotNull(averageScoreProp);

            int auditsTotal = (int)auditsTotalProp.GetValue(val)!;
            decimal averageScore = (decimal)averageScoreProp.GetValue(val)!;

            Assert.Equal(2, auditsTotal);
            Assert.Equal(95.0m, averageScore);
        }
    }
}
