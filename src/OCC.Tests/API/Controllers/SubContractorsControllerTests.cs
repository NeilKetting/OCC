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
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class SubContractorsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<SubContractorsController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public SubContractorsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<SubContractorsController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
        }

        [Fact]
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            using var context = new AppDbContext(_dbOptions);
            Assert.Throws<ArgumentNullException>(() => new SubContractorsController(null!, _mockHubContext.Object, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new SubContractorsController(context, null!, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new SubContractorsController(context, _mockHubContext.Object, null!));
        }

        [Fact]
        public async Task GetSubContractorSummaries_ReturnsOkWithSummaries()
        {
            using var context = new AppDbContext(_dbOptions);
            context.SubContractors.Add(new SubContractor
            {
                Id = Guid.NewGuid(),
                Name = "Apex Concrete",
                Specialties = "Civil Works",
                ColorTheme = "#FF0000"
            });
            await context.SaveChangesAsync();

            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.GetSubContractorSummaries();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var summaries = Assert.IsAssignableFrom<IEnumerable<SubContractorSummaryDto>>(okResult.Value);
            Assert.Single(summaries);
            Assert.Equal("Apex Concrete", summaries.First().Name);
        }

        [Fact]
        public async Task GetSubContractors_ReturnsAllSubContractors()
        {
            using var context = new AppDbContext(_dbOptions);
            context.SubContractors.Add(new SubContractor { Id = Guid.NewGuid(), Name = "Sub 1" });
            context.SubContractors.Add(new SubContractor { Id = Guid.NewGuid(), Name = "Sub 2" });
            await context.SaveChangesAsync();

            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.GetSubContractors();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var subs = Assert.IsAssignableFrom<IEnumerable<SubContractor>>(okResult.Value);
            Assert.Equal(2, subs.Count());
        }

        [Fact]
        public async Task GetSubContractor_EmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetSubContractor(Guid.Empty);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetSubContractor_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetSubContractor(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetSubContractor_ValidId_ReturnsSubContractor()
        {
            using var context = new AppDbContext(_dbOptions);
            var id = Guid.NewGuid();
            context.SubContractors.Add(new SubContractor { Id = id, Name = "BuildTech Plumbing" });
            await context.SaveChangesAsync();

            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.GetSubContractor(id);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var sub = Assert.IsType<SubContractor>(okResult.Value);
            Assert.Equal(id, sub.Id);
        }

        [Fact]
        public async Task PostSubContractor_NullOrEmptyName_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);

            var nullRes = await controller.PostSubContractor(null!);
            Assert.IsType<BadRequestObjectResult>(nullRes.Result);

            var emptyNameSub = new SubContractor { Name = "  " };
            var emptyRes = await controller.PostSubContractor(emptyNameSub);
            Assert.IsType<BadRequestObjectResult>(emptyRes.Result);
        }

        [Fact]
        public async Task PostSubContractor_Valid_CreatesAndEmitsSignalR()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);

            var sub = new SubContractor
            {
                Name = "Precision Roofing",
                Specialties = "Roofing",
                ColorTheme = "#00FF00"
            };

            var result = await controller.PostSubContractor(sub);
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returned = Assert.IsType<SubContractor>(created.Value);

            Assert.NotEqual(Guid.Empty, returned.Id);
            Assert.Equal("Precision Roofing", returned.Name);

            _mockClientProxy.Verify(c => c.SendCoreAsync("EntityUpdate", It.Is<object[]>(o => o[0].ToString() == "SubContractor" && o[1].ToString() == "Create"), default), Times.Once);
        }

        [Fact]
        public async Task PutSubContractor_NullOrMismatchId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);

            var sub = new SubContractor { Id = Guid.NewGuid(), Name = "Sub" };
            var result = await controller.PutSubContractor(Guid.NewGuid(), sub);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task PutSubContractor_ValidUpdate_UpdatesAndReturnsNoContent()
        {
            using var context = new AppDbContext(_dbOptions);
            var id = Guid.NewGuid();
            context.SubContractors.Add(new SubContractor { Id = id, Name = "Old SubName", ColorTheme = "#000000" });
            await context.SaveChangesAsync();

            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);
            var updatePayload = new SubContractor { Id = id, Name = "Updated SubName", ColorTheme = "#FFFFFF" };

            var result = await controller.PutSubContractor(id, updatePayload);
            Assert.IsType<NoContentResult>(result);

            var dbSub = await context.SubContractors.FindAsync(id);
            Assert.NotNull(dbSub);
            Assert.Equal("Updated SubName", dbSub.Name);
            Assert.Equal("#FFFFFF", dbSub.ColorTheme);
        }

        [Fact]
        public async Task DeleteSubContractor_EmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.DeleteSubContractor(Guid.Empty);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteSubContractor_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.DeleteSubContractor(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteSubContractor_ValidId_DeletesAndEmitsSignalR()
        {
            using var context = new AppDbContext(_dbOptions);
            var id = Guid.NewGuid();
            context.SubContractors.Add(new SubContractor { Id = id, Name = "ToDelete Sub" });
            await context.SaveChangesAsync();

            var controller = new SubContractorsController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.DeleteSubContractor(id);

            Assert.IsType<NoContentResult>(result);
            var dbSub = await context.SubContractors.FindAsync(id);
            Assert.False(dbSub!.IsActive);

            _mockClientProxy.Verify(c => c.SendCoreAsync("EntityUpdate", It.Is<object[]>(o => o[0].ToString() == "SubContractor" && o[1].ToString() == "Delete"), default), Times.Once);
        }
    }
}
