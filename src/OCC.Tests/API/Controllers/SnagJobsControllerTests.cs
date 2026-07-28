using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class SnagJobsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<SnagJobsController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public SnagJobsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<SnagJobsController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
            _mockClientProxy
                .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);
        }

        private static ControllerContext CreateControllerContext(string userId, string role = "SiteManager")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            };
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
            };
        }

        [Fact]
        public async Task GetSnagJobs_ReturnsAllSnagJobs()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SnagJobsController(context, _mockHubContext.Object, _mockLogger.Object);

            var pId = Guid.NewGuid();
            var subId = Guid.NewGuid();
            context.Projects.Add(new Project { Id = pId, Name = "P1" });
            context.SubContractors.Add(new SubContractor { Id = subId, Name = "Sub1" });

            context.SnagJobs.Add(new SnagJob { Id = Guid.NewGuid(), ProjectId = pId, SubContractorId = subId, Title = "Snag 1" });
            context.SnagJobs.Add(new SnagJob { Id = Guid.NewGuid(), ProjectId = pId, SubContractorId = subId, Title = "Snag 2" });
            await context.SaveChangesAsync();

            var result = await controller.GetSnagJobs();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var snags = Assert.IsAssignableFrom<IEnumerable<SnagJob>>(okResult.Value);
            Assert.Equal(2, snags.Count());
        }

        [Fact]
        public async Task GetProjectSnagJobs_ReturnsSnagsForProject()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SnagJobsController(context, _mockHubContext.Object, _mockLogger.Object);

            var p1 = Guid.NewGuid();
            var p2 = Guid.NewGuid();
            var subId = Guid.NewGuid();

            context.Projects.Add(new Project { Id = p1, Name = "P1" });
            context.Projects.Add(new Project { Id = p2, Name = "P2" });
            context.SubContractors.Add(new SubContractor { Id = subId, Name = "Sub1" });

            context.SnagJobs.Add(new SnagJob { Id = Guid.NewGuid(), ProjectId = p1, SubContractorId = subId, Title = "Snag P1" });
            context.SnagJobs.Add(new SnagJob { Id = Guid.NewGuid(), ProjectId = p2, SubContractorId = subId, Title = "Snag P2" });
            await context.SaveChangesAsync();

            var result = await controller.GetProjectSnagJobs(p1);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var snags = Assert.IsAssignableFrom<IEnumerable<SnagJob>>(okResult.Value);
            Assert.Single(snags);
            Assert.Equal("Snag P1", snags.First().Title);
        }

        [Fact]
        public async Task GetSubContractorSnagJobs_ReturnsSnagsForSubContractor()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SnagJobsController(context, _mockHubContext.Object, _mockLogger.Object);

            var pId = Guid.NewGuid();
            var sub1 = Guid.NewGuid();
            var sub2 = Guid.NewGuid();

            context.Projects.Add(new Project { Id = pId, Name = "P1" });
            context.SubContractors.Add(new SubContractor { Id = sub1, Name = "Sub1" });
            context.SubContractors.Add(new SubContractor { Id = sub2, Name = "Sub2" });

            context.SnagJobs.Add(new SnagJob { Id = Guid.NewGuid(), ProjectId = pId, SubContractorId = sub1, Title = "Sub 1 Snag" });
            context.SnagJobs.Add(new SnagJob { Id = Guid.NewGuid(), ProjectId = pId, SubContractorId = sub2, Title = "Sub 2 Snag" });
            await context.SaveChangesAsync();

            var result = await controller.GetSubContractorSnagJobs(sub1);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var snags = Assert.IsAssignableFrom<IEnumerable<SnagJob>>(okResult.Value);
            Assert.Single(snags);
            Assert.Equal("Sub 1 Snag", snags.First().Title);
        }

        [Fact]
        public async Task GetSnagJob_ValidId_ReturnsSnagJob()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SnagJobsController(context, _mockHubContext.Object, _mockLogger.Object);

            var id = Guid.NewGuid();
            var pId = Guid.NewGuid();
            var subId = Guid.NewGuid();

            context.Projects.Add(new Project { Id = pId, Name = "P1" });
            context.SubContractors.Add(new SubContractor { Id = subId, Name = "Sub1" });
            context.SnagJobs.Add(new SnagJob { Id = id, ProjectId = pId, SubContractorId = subId, Title = "Target Snag" });
            await context.SaveChangesAsync();

            var result = await controller.GetSnagJob(id);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var snag = Assert.IsType<SnagJob>(okResult.Value);
            Assert.Equal("Target Snag", snag.Title);
        }

        [Fact]
        public async Task GetSnagJob_InvalidId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SnagJobsController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetSnagJob(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostSnagJob_ValidInput_SanitizesAndCreatesSnagJobAndRecalculatesRating()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SnagJobsController(context, _mockHubContext.Object, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "SiteManager");

            var subId = Guid.NewGuid();
            var pId = Guid.NewGuid();
            var contractor = new SubContractor { Id = subId, Name = "Plumbing Co", OnTimeRate = 0.9m, CompletedTasksCount = 5 };
            context.SubContractors.Add(contractor);
            context.Projects.Add(new Project { Id = pId, Name = "P1" });
            await context.SaveChangesAsync();

            var snag = new SnagJob
            {
                Id = Guid.NewGuid(),
                ProjectId = pId,
                SubContractorId = subId,
                Title = "  <script>alert('xss')</script>Leaky Pipe  ",
                Description = "Pipe leaking near <b>sink</b>",
                Status = SnagStatus.Open
            };

            var result = await controller.PostSnagJob(snag);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdSnag = Assert.IsType<SnagJob>(createdResult.Value);
            Assert.Equal("Leaky Pipe", createdSnag.Title);
            Assert.Equal("Pipe leaking near sink", createdSnag.Description);

            var dbContractor = await context.SubContractors.FindAsync(subId);
            Assert.Equal(1, dbContractor!.TotalSnagsCount);
        }

        [Fact]
        public async Task PutSnagJob_ValidUpdate_UpdatesSnagJobAndCompletionDate()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SnagJobsController(context, _mockHubContext.Object, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "SiteManager");

            var id = Guid.NewGuid();
            var pId = Guid.NewGuid();
            var subId = Guid.NewGuid();
            context.Projects.Add(new Project { Id = pId, Name = "P1" });
            context.SubContractors.Add(new SubContractor { Id = subId, Name = "Paint Co", OnTimeRate = 1.0m });
            context.SnagJobs.Add(new SnagJob { Id = id, ProjectId = pId, SubContractorId = subId, Title = "Paint defect", Status = SnagStatus.Open });
            await context.SaveChangesAsync();

            var updatePayload = new SnagJob
            {
                Id = id,
                ProjectId = pId,
                SubContractorId = subId,
                Title = "Paint defect fixed",
                Status = SnagStatus.Fixed
            };

            var result = await controller.PutSnagJob(id, updatePayload);

            Assert.IsType<NoContentResult>(result);

            var dbSnag = await context.SnagJobs.FindAsync(id);
            Assert.NotNull(dbSnag);
            Assert.Equal(SnagStatus.Fixed, dbSnag.Status);
            Assert.NotNull(dbSnag.CompletionDate);
        }

        [Fact]
        public async Task PutSnagJob_MismatchId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SnagJobsController(context, _mockHubContext.Object, _mockLogger.Object);

            var snag = new SnagJob { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), SubContractorId = Guid.NewGuid(), Title = "Mismatch" };
            var result = await controller.PutSnagJob(Guid.NewGuid(), snag);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteSnagJob_ValidId_DeletesSnagJobAndRecalculatesRating()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SnagJobsController(context, _mockHubContext.Object, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "Admin");

            var id = Guid.NewGuid();
            var pId = Guid.NewGuid();
            var subId = Guid.NewGuid();
            context.Projects.Add(new Project { Id = pId, Name = "P1" });
            context.SubContractors.Add(new SubContractor { Id = subId, Name = "Sub", TotalSnagsCount = 1 });
            context.SnagJobs.Add(new SnagJob { Id = id, ProjectId = pId, SubContractorId = subId, Title = "ToDelete" });
            await context.SaveChangesAsync();

            var result = await controller.DeleteSnagJob(id);

            Assert.IsType<NoContentResult>(result);
            var dbSnag = await context.SnagJobs.FirstOrDefaultAsync(s => s.Id == id);
            Assert.Null(dbSnag);

            var dbContractor = await context.SubContractors.FindAsync(subId);
            Assert.Equal(0, dbContractor!.TotalSnagsCount);
        }
    }
}
