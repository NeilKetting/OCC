using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.API.Services;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        [Fact]
        public async Task ImportTasks_ClearsExistingTasksAndSavesNewTasks_Successfully()
        {
            // Arrange
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

            // Act
            var result = await controller.ImportTasks(project.Id, newTasks);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            // Check that the db has only the new tasks for this project
            var dbTasks = await context.ProjectTasks.Where(t => t.ProjectId == project.Id).ToListAsync();
            Assert.Equal(2, dbTasks.Count);
            Assert.Contains(dbTasks, t => t.Name == "New Task 1");
            Assert.Contains(dbTasks, t => t.Name == "New Task 2");
            Assert.DoesNotContain(dbTasks, t => t.Name == "Existing Task");
        }
    }
}
