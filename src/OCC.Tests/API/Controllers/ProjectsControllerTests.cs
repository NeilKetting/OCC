using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.API.Services;
using OCC.Shared.DTOs;
using OCC.Shared.Framework;
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
    public class ProjectsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<ProjectsController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public ProjectsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<ProjectsController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockNotificationService = new Mock<INotificationService>();
            
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
            _mockClientProxy
                .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);
        }

        private static ControllerContext CreateControllerContext(string userId, string email, string role = "Admin")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        [Fact]
        public async Task GetProjectSummaries_ReturnsSummariesWithCalculatedProgressAndStatus()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Project Alpha",
                Status = "Planning",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                CreatedBy = "user@occ.com"
            };
            project.Tasks.Add(new ProjectTask { Id = Guid.NewGuid(), Name = "Task 1", PercentComplete = 100 });
            project.Tasks.Add(new ProjectTask { Id = Guid.NewGuid(), Name = "Task 2", PercentComplete = 50 });

            context.Projects.Add(project);
            await context.SaveChangesAsync();

            var result = await controller.GetProjectSummaries();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var summaries = Assert.IsAssignableFrom<IEnumerable<ProjectSummaryDto>>(okResult.Value);
            var summary = summaries.FirstOrDefault(s => s.Id == project.Id);
            Assert.NotNull(summary);
            Assert.Equal("Project Alpha", summary.Name);
            Assert.Equal(75, summary.Progress);
            Assert.Equal("In Progress", summary.Status);
        }

        [Fact]
        public async Task GetProjects_ReturnsAllProjects()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "admin@occ.com", "Admin");

            context.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "P1" });
            context.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "P2" });
            await context.SaveChangesAsync();

            var result = await controller.GetProjects();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var projects = Assert.IsAssignableFrom<IEnumerable<Project>>(okResult.Value);
            Assert.Equal(2, projects.Count());
        }

        [Fact]
        public async Task GetProjects_FilterAssignedToMe_Admin_ReturnsAll()
        {
            using var context = new AppDbContext(_dbOptions);
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, Email = "admin@occ.com", UserRole = UserRole.Admin, FirstName = "Admin", LastName = "User" };
            context.Users.Add(user);
            context.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "Admin Project" });
            await context.SaveChangesAsync();

            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);
            controller.ControllerContext = CreateControllerContext(userId.ToString(), "admin@occ.com", "Admin");

            var result = await controller.GetProjects(assignedToMe: true);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var projects = Assert.IsAssignableFrom<IEnumerable<Project>>(okResult.Value);
            Assert.Single(projects);
        }

        [Fact]
        public async Task GetProject_ValidId_ReturnsProject()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);
            var projectId = Guid.NewGuid();
            context.Projects.Add(new Project { Id = projectId, Name = "Target Project", CreatedBy = "creator@occ.com" });
            await context.SaveChangesAsync();

            var result = await controller.GetProject(projectId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var project = Assert.IsType<Project>(okResult.Value);
            Assert.Equal("Target Project", project.Name);
        }

        [Fact]
        public async Task GetProject_EmptyId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);

            var result = await controller.GetProject(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetProject_NotFound_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);

            var result = await controller.GetProject(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task PostProject_ValidInput_SanitizesStringsAndCreatesProject()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);
            var userId = Guid.NewGuid().ToString();
            controller.ControllerContext = CreateControllerContext(userId, "pm@occ.com", "Office");

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "  <script>alert('xss')</script>Clean Project  ",
                Description = "Description <b>Safe</b>",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(60)
            };

            var result = await controller.PostProject(project);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdProject = Assert.IsType<Project>(createdResult.Value);
            Assert.Equal("Clean Project", createdProject.Name);
            Assert.Equal("Description Safe", createdProject.Description);
        }

        [Fact]
        public async Task PutProject_ValidUpdate_UpdatesProject()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "office@occ.com", "Office");

            var projectId = Guid.NewGuid();
            var existing = new Project { Id = projectId, Name = "Old Name" };
            context.Projects.Add(existing);
            await context.SaveChangesAsync();

            var updatePayload = new Project { Id = projectId, Name = "Updated Name", Description = "Updated Desc" };

            var result = await controller.PutProject(projectId, updatePayload);

            Assert.IsType<NoContentResult>(result);
            var dbProject = await context.Projects.FindAsync(projectId);
            Assert.Equal("Updated Name", dbProject!.Name);
        }

        [Fact]
        public async Task PutProject_MismatchId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);

            var project = new Project { Id = Guid.NewGuid(), Name = "Test" };
            var result = await controller.PutProject(Guid.NewGuid(), project);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteProject_SoftDelete_SetsIsActiveFalse()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "admin@occ.com", "Admin");

            var projectId = Guid.NewGuid();
            context.Projects.Add(new Project { Id = projectId, Name = "ToDelete", IsActive = true });
            await context.SaveChangesAsync();

            var result = await controller.DeleteProject(projectId, permanent: false);

            Assert.IsType<NoContentResult>(result);
            var dbProject = await context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == projectId);
            Assert.NotNull(dbProject);
            Assert.False(dbProject.IsActive);
        }

        [Fact]
        public async Task RestoreProject_InactiveProject_RestoresProject()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "admin@occ.com", "Admin");

            var projectId = Guid.NewGuid();
            var project = new Project { Id = projectId, Name = "SoftDeleted" };
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            project.IsActive = false;
            await context.SaveChangesAsync();

            var result = await controller.RestoreProject(projectId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var restored = Assert.IsType<Project>(okResult.Value);
            Assert.True(restored.IsActive);
        }

        [Fact]
        public async Task RestoreProject_AlreadyActive_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "admin@occ.com", "Admin");

            var projectId = Guid.NewGuid();
            context.Projects.Add(new Project { Id = projectId, Name = "ActiveProject", IsActive = true });
            await context.SaveChangesAsync();

            var result = await controller.RestoreProject(projectId);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetProjectPersonnel_ReturnsPersonnelDto()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);

            var empId = Guid.NewGuid();
            var emp = new Employee { Id = empId, FirstName = "John", LastName = "Doe", Role = EmployeeRole.Supervisor, Status = EmployeeStatus.Active };
            var projectId = Guid.NewGuid();
            var project = new Project { Id = projectId, Name = "Personnel Project", SiteManagerId = empId, SiteManager = emp };
            project.TeamMembers.Add(new ProjectTeamMember { Id = Guid.NewGuid(), ProjectId = projectId, EmployeeId = empId, Employee = emp });

            context.Employees.Add(emp);
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            var result = await controller.GetProjectPersonnel(projectId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ProjectPersonnelDto>(okResult.Value);
            Assert.Equal(projectId, dto.ProjectId);
            Assert.Equal(empId, dto.SiteManagerId);
            Assert.Single(dto.TeamMembers);
        }

        [Fact]
        public async Task UpdateProjectPersonnel_UpdatesSiteManagerAndTeamMembers()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "admin@occ.com", "Admin");

            var projectId = Guid.NewGuid();
            var emp1 = Guid.NewGuid();
            var emp2 = Guid.NewGuid();

            context.Employees.Add(new Employee { Id = emp1, FirstName = "Emp", LastName = "One" });
            context.Employees.Add(new Employee { Id = emp2, FirstName = "Emp", LastName = "Two" });
            context.Projects.Add(new Project { Id = projectId, Name = "Team Project" });
            await context.SaveChangesAsync();

            var updateDto = new ProjectPersonnelUpdateDto
            {
                SiteManagerId = emp1,
                TeamMemberIds = new List<Guid> { emp1, emp2 }
            };

            var result = await controller.UpdateProjectPersonnel(projectId, updateDto);

            Assert.IsType<NoContentResult>(result);
            var dbProject = await context.Projects.Include(p => p.TeamMembers).FirstOrDefaultAsync(p => p.Id == projectId);
            Assert.Equal(emp1, dbProject!.SiteManagerId);
            Assert.Equal(2, dbProject.TeamMembers.Count);
        }

        [Fact]
        public async Task ImportTasks_ClearsExistingTasksAndSavesNewTasks_Successfully()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Test Project"
            };

            var existingTask = new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = "Existing Task"
            };

            context.Projects.Add(project);
            context.ProjectTasks.Add(existingTask);
            await context.SaveChangesAsync();

            var newTasks = new List<ProjectTask>
            {
                new() { Id = Guid.NewGuid(), Name = "New Task 1" },
                new() { Id = Guid.NewGuid(), Name = "New Task 2" }
            };

            var result = await controller.ImportTasks(project.Id, newTasks);

            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var dbTasks = await context.ProjectTasks.Where(t => t.ProjectId == project.Id).ToListAsync();
            Assert.Equal(2, dbTasks.Count);
            Assert.Contains(dbTasks, t => t.Name == "New Task 1");
            Assert.Contains(dbTasks, t => t.Name == "New Task 2");
            Assert.DoesNotContain(dbTasks, t => t.Name == "Existing Task");
        }

        [Fact]
        public async Task GetProjectsPaged_ReturnsPagedApiResponse()
        {
            using var context = new AppDbContext(_dbOptions);
            context.Projects.AddRange(
                new Project { Id = Guid.NewGuid(), Name = "Alpha Project", Code = "PRJ-001" },
                new Project { Id = Guid.NewGuid(), Name = "Beta Project", Code = "PRJ-002" }
            );
            await context.SaveChangesAsync();

            var controller = new ProjectsController(context, _mockHubContext.Object, _mockLogger.Object, _mockNotificationService.Object);

            var actionResult = await controller.GetProjectsPaged(page: 1, pageSize: 10);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var apiResponse = Assert.IsType<ApiResponse<PagedResult<ProjectSummaryDto>>>(okResult.Value);

            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(2, apiResponse.Data.TotalCount);
        }
    }
}
