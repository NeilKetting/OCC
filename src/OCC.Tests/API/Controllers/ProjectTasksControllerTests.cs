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
using OCC.Shared.Enums;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class ProjectTasksControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<ProjectTasksController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public ProjectTasksControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<ProjectTasksController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
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
        public async Task GetProjectTasks_ReturnsAllTasks()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectTasksController(context, _mockHubContext.Object, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "user@occ.com", "Office");

            context.ProjectTasks.Add(new ProjectTask { Id = Guid.NewGuid(), Name = "Task 1" });
            context.ProjectTasks.Add(new ProjectTask { Id = Guid.NewGuid(), Name = "Task 2" });
            await context.SaveChangesAsync();

            var result = await controller.GetProjectTasks();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var tasks = Assert.IsAssignableFrom<IEnumerable<ProjectTask>>(okResult.Value);
            Assert.Equal(2, tasks.Count());
        }

        [Fact]
        public async Task GetProjectTasks_FilterByProjectId_ReturnsFilteredTasks()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectTasksController(context, _mockHubContext.Object, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "user@occ.com", "Office");

            var p1 = Guid.NewGuid();
            var p2 = Guid.NewGuid();

            context.ProjectTasks.Add(new ProjectTask { Id = Guid.NewGuid(), ProjectId = p1, Name = "Task P1" });
            context.ProjectTasks.Add(new ProjectTask { Id = Guid.NewGuid(), ProjectId = p2, Name = "Task P2" });
            await context.SaveChangesAsync();

            var result = await controller.GetProjectTasks(projectId: p1);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var tasks = Assert.IsAssignableFrom<IEnumerable<ProjectTask>>(okResult.Value);
            Assert.Single(tasks);
            Assert.Equal("Task P1", tasks.First().Name);
        }

        [Fact]
        public async Task GetSubContractorTasks_ReturnsTasksAssignedToContractor()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectTasksController(context, _mockHubContext.Object, _mockLogger.Object);

            var subId = Guid.NewGuid();
            var task = new ProjectTask { Id = Guid.NewGuid(), Name = "SubTask" };
            task.Assignments.Add(new TaskAssignment
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                AssigneeId = subId,
                AssigneeType = AssigneeType.Contractor
            });

            context.ProjectTasks.Add(task);
            await context.SaveChangesAsync();

            var result = await controller.GetSubContractorTasks(subId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var tasks = Assert.IsAssignableFrom<IEnumerable<ProjectTask>>(okResult.Value);
            Assert.Single(tasks);
            Assert.Equal("SubTask", tasks.First().Name);
        }

        [Fact]
        public async Task GetProjectTask_ValidId_ReturnsTask()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectTasksController(context, _mockHubContext.Object, _mockLogger.Object);

            var taskId = Guid.NewGuid();
            context.ProjectTasks.Add(new ProjectTask { Id = taskId, Name = "Single Task" });
            await context.SaveChangesAsync();

            var result = await controller.GetProjectTask(taskId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedTask = Assert.IsType<ProjectTask>(okResult.Value);
            Assert.Equal("Single Task", returnedTask.Name);
        }

        [Fact]
        public async Task GetProjectTask_InvalidId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectTasksController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetProjectTask(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostProjectTask_ValidInput_SanitizesAndCreatesTask()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectTasksController(context, _mockHubContext.Object, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "user@occ.com", "Office");

            var task = new ProjectTask
            {
                Id = Guid.NewGuid(),
                Name = "  <script>alert('xss')</script>Build Foundations  ",
                Description = "Excavation <b>safe</b>",
                StartDate = DateTime.UtcNow,
                FinishDate = DateTime.UtcNow.AddDays(5),
                PercentComplete = 150
            };

            var result = await controller.PostProjectTask(task);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdTask = Assert.IsType<ProjectTask>(createdResult.Value);
            Assert.Equal("Build Foundations", createdTask.Name);
            Assert.Equal("Excavation safe", createdTask.Description);
            Assert.Equal(100, createdTask.PercentComplete);
        }

        [Fact]
        public async Task PutProjectTask_ValidUpdate_UpdatesTaskAndPerformsParentRollup()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectTasksController(context, _mockHubContext.Object, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "user@occ.com", "SiteManager");

            var parentId = Guid.NewGuid();
            var parentTask = new ProjectTask { Id = parentId, Name = "Parent Group", IsGroup = true, PercentComplete = 0 };

            var childId = Guid.NewGuid();
            var childTask = new ProjectTask { Id = childId, ParentId = parentId, Name = "Child Task", PercentComplete = 0, Status = "To Do" };

            context.ProjectTasks.Add(parentTask);
            context.ProjectTasks.Add(childTask);
            await context.SaveChangesAsync();

            var updateChild = new ProjectTask
            {
                Id = childId,
                ParentId = parentId,
                Name = "Child Task Updated",
                PercentComplete = 100,
                Status = "Completed"
            };

            var result = await controller.PutProjectTask(childId, updateChild);

            Assert.IsType<NoContentResult>(result);

            var dbParent = await context.ProjectTasks.FindAsync(parentId);
            Assert.NotNull(dbParent);
            Assert.Equal(100, dbParent.PercentComplete);
            Assert.Equal("Done", dbParent.Status);
        }

        [Fact]
        public async Task PutProjectTask_MismatchId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectTasksController(context, _mockHubContext.Object, _mockLogger.Object);

            var task = new ProjectTask { Id = Guid.NewGuid(), Name = "Task" };
            var result = await controller.PutProjectTask(Guid.NewGuid(), task);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteProjectTask_ValidId_DeletesTask()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectTasksController(context, _mockHubContext.Object, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "admin@occ.com", "Admin");

            var taskId = Guid.NewGuid();
            context.ProjectTasks.Add(new ProjectTask { Id = taskId, Name = "TaskToDelete" });
            await context.SaveChangesAsync();

            var result = await controller.DeleteProjectTask(taskId);

            Assert.IsType<NoContentResult>(result);
            var dbTask = await context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            Assert.Null(dbTask);
        }
    }
}
