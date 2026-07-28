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
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class SiteDeploymentsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<SiteDeploymentsController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public SiteDeploymentsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<SiteDeploymentsController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
        }

        private static ControllerContext CreateControllerContext(string userId, string email, string role = "SiteManager")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Role, role)
            };
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
            };
        }

        [Fact]
        public async Task GetDeployments_ReturnsAllDeployments()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SiteDeploymentsController(context, _mockLogger.Object, _mockHubContext.Object);

            var projectId = Guid.NewGuid();
            context.Projects.Add(new Project { Id = projectId, Name = "Site A" });
            context.SiteDeployments.Add(new SiteDeployment { Id = Guid.NewGuid(), ProjectId = projectId, Label = "Crew 1" });
            await context.SaveChangesAsync();

            var result = await controller.GetDeployments();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var deployments = Assert.IsAssignableFrom<IEnumerable<SiteDeploymentDto>>(okResult.Value);
            Assert.Single(deployments);
        }

        [Fact]
        public async Task GetDeployment_ValidId_ReturnsDto()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SiteDeploymentsController(context, _mockLogger.Object, _mockHubContext.Object);

            var id = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            context.Projects.Add(new Project { Id = projectId, Name = "Site B" });
            context.SiteDeployments.Add(new SiteDeployment { Id = id, ProjectId = projectId, Label = "Morning Crew" });
            await context.SaveChangesAsync();

            var result = await controller.GetDeployment(id);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SiteDeploymentDto>(okResult.Value);
            Assert.Equal("Morning Crew", dto.Label);
        }

        [Fact]
        public async Task CreateDeployment_ValidInput_CreatesDeploymentAndMembers()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SiteDeploymentsController(context, _mockLogger.Object, _mockHubContext.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "sm@occ.com");

            var projectId = Guid.NewGuid();
            var empId = Guid.NewGuid();

            context.Projects.Add(new Project { Id = projectId, Name = "Site C" });
            context.Employees.Add(new Employee { Id = empId, FirstName = "Worker", LastName = "One" });
            await context.SaveChangesAsync();

            var request = new CreateSiteDeploymentRequest
            {
                ProjectId = projectId,
                DeploymentDate = DateTime.UtcNow.Date,
                Label = "  <script>alert('xss')</script>Concrete Crew  ",
                MemberEmployeeIds = new List<Guid> { empId }
            };

            var result = await controller.CreateDeployment(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<SiteDeploymentDto>(createdResult.Value);
            Assert.Equal("Concrete Crew", dto.Label);
            Assert.Single(dto.Members);
        }

        [Fact]
        public async Task CreateDeployment_InvalidProject_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SiteDeploymentsController(context, _mockLogger.Object, _mockHubContext.Object);

            var request = new CreateSiteDeploymentRequest
            {
                ProjectId = Guid.NewGuid(),
                DeploymentDate = DateTime.UtcNow,
                Label = "Crew"
            };

            var result = await controller.CreateDeployment(request);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task UpdateDeployment_NonPendingStatus_ReturnsConflict()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SiteDeploymentsController(context, _mockLogger.Object, _mockHubContext.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "sm@occ.com");

            var id = Guid.NewGuid();
            context.SiteDeployments.Add(new SiteDeployment { Id = id, Status = DeploymentStatus.Received });
            await context.SaveChangesAsync();

            var request = new CreateSiteDeploymentRequest { ProjectId = Guid.NewGuid(), Label = "Update" };

            var result = await controller.UpdateDeployment(id, request);

            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task CancelDeployment_PendingStatus_CancelsDeployment()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SiteDeploymentsController(context, _mockLogger.Object, _mockHubContext.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "sm@occ.com");

            var id = Guid.NewGuid();
            context.SiteDeployments.Add(new SiteDeployment { Id = id, Status = DeploymentStatus.Pending });
            await context.SaveChangesAsync();

            var result = await controller.CancelDeployment(id);

            Assert.IsType<NoContentResult>(result);
            var dbDeployment = await context.SiteDeployments.FindAsync(id);
            Assert.Equal(DeploymentStatus.Cancelled, dbDeployment!.Status);
        }

        [Fact]
        public async Task ReceiveDeployment_ValidRequest_UpdatesStatusRecordsGpsAndDistance()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SiteDeploymentsController(context, _mockLogger.Object, _mockHubContext.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "sm@occ.com");

            var projectId = Guid.NewGuid();
            var smId = Guid.NewGuid();
            var empId = Guid.NewGuid();

            var project = new Project { Id = projectId, Name = "Site D", Latitude = -23.9036, Longitude = 29.4689 };
            var deployment = new SiteDeployment { Id = Guid.NewGuid(), ProjectId = projectId, Status = DeploymentStatus.Pending };
            deployment.Members.Add(new SiteDeploymentMember { Id = Guid.NewGuid(), SiteDeploymentId = deployment.Id, EmployeeId = empId });

            context.Projects.Add(project);
            context.SiteDeployments.Add(deployment);
            context.AttendanceRecords.Add(new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = empId, Date = DateTime.UtcNow.Date });
            await context.SaveChangesAsync();

            var receiveRequest = new ReceiveDeploymentRequest
            {
                SiteManagerId = smId,
                GpsLatitude = -23.9037,
                GpsLongitude = 29.4690,
                AbsentMemberEmployeeIds = new List<Guid>()
            };

            var result = await controller.ReceiveDeployment(deployment.Id, receiveRequest);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var dbDeployment = await context.SiteDeployments.FindAsync(deployment.Id);
            Assert.Equal(DeploymentStatus.Received, dbDeployment!.Status);
            Assert.NotNull(dbDeployment.ReceivedAt);
            Assert.NotNull(dbDeployment.DistanceFromSiteMetres);
        }

        [Fact]
        public async Task ReceiveDeployment_InvalidGps_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SiteDeploymentsController(context, _mockLogger.Object, _mockHubContext.Object);

            var receiveRequest = new ReceiveDeploymentRequest
            {
                SiteManagerId = Guid.NewGuid(),
                GpsLatitude = 120.0, // Invalid latitude > 90
                GpsLongitude = 29.0
            };

            var result = await controller.ReceiveDeployment(Guid.NewGuid(), receiveRequest);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetTodayClockedIn_ReturnsClockedInEmployees()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SiteDeploymentsController(context, _mockLogger.Object, _mockHubContext.Object);

            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee { Id = empId, FirstName = "Jane", LastName = "Smith", Status = EmployeeStatus.Active });
            context.AttendanceRecords.Add(new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = empId, Date = DateTime.UtcNow.Date });
            await context.SaveChangesAsync();

            var result = await controller.GetTodayClockedIn();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dtos = Assert.IsAssignableFrom<IEnumerable<EmployeeSummaryDto>>(okResult.Value);
            Assert.Single(dtos);
            Assert.Equal("Jane", dtos.First().FirstName);
        }
    }
}
