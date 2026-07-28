using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers.Projects;
using OCC.API.Data;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class ProjectVariationOrdersControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<ProjectVariationOrdersController>> _mockLogger;

        public ProjectVariationOrdersControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<ProjectVariationOrdersController>>();
        }

        private static ControllerContext CreateControllerContext(string userId, string role = "Office")
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
        public async Task GetVariationOrders_ReturnsAllVariationOrders()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectVariationOrdersController(context, _mockLogger.Object);

            var pId = Guid.NewGuid();
            context.ProjectVariationOrders.Add(new ProjectVariationOrder { Id = Guid.NewGuid(), ProjectId = pId, Description = "VO 1" });
            context.ProjectVariationOrders.Add(new ProjectVariationOrder { Id = Guid.NewGuid(), ProjectId = pId, Description = "VO 2" });
            await context.SaveChangesAsync();

            var result = await controller.GetVariationOrders();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var orders = Assert.IsAssignableFrom<IEnumerable<ProjectVariationOrder>>(okResult.Value);
            Assert.Equal(2, orders.Count());
        }

        [Fact]
        public async Task GetVariationOrders_FilteredByProjectId_ReturnsFiltered()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectVariationOrdersController(context, _mockLogger.Object);

            var p1 = Guid.NewGuid();
            var p2 = Guid.NewGuid();
            context.ProjectVariationOrders.Add(new ProjectVariationOrder { Id = Guid.NewGuid(), ProjectId = p1, Description = "VO P1" });
            context.ProjectVariationOrders.Add(new ProjectVariationOrder { Id = Guid.NewGuid(), ProjectId = p2, Description = "VO P2" });
            await context.SaveChangesAsync();

            var result = await controller.GetVariationOrders(projectId: p1);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var orders = Assert.IsAssignableFrom<IEnumerable<ProjectVariationOrder>>(okResult.Value);
            Assert.Single(orders);
            Assert.Equal("VO P1", orders.First().Description);
        }

        [Fact]
        public async Task GetVariationOrder_ValidId_ReturnsVariationOrder()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectVariationOrdersController(context, _mockLogger.Object);

            var id = Guid.NewGuid();
            context.ProjectVariationOrders.Add(new ProjectVariationOrder { Id = id, Description = "Single VO" });
            await context.SaveChangesAsync();

            var result = await controller.GetVariationOrder(id);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var vo = Assert.IsType<ProjectVariationOrder>(okResult.Value);
            Assert.Equal("Single VO", vo.Description);
        }

        [Fact]
        public async Task GetVariationOrder_InvalidId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectVariationOrdersController(context, _mockLogger.Object);

            var result = await controller.GetVariationOrder(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetVariationOrder_NotFound_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectVariationOrdersController(context, _mockLogger.Object);

            var result = await controller.GetVariationOrder(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task PostVariationOrder_ValidInput_SanitizesAndCreatesVO()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectVariationOrdersController(context, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "Office");

            var vo = new ProjectVariationOrder
            {
                Id = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                Description = "  <script>alert('xss')</script>Extra Paving Work  ",
                ApprovedBy = "John Manager",
                Status = "Approved",
                DurationDays = -5
            };

            var result = await controller.PostVariationOrder(vo);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdVO = Assert.IsType<ProjectVariationOrder>(createdResult.Value);
            Assert.Equal("Extra Paving Work", createdVO.Description);
            Assert.Equal(0, createdVO.DurationDays);
        }

        [Fact]
        public async Task PostVariationOrder_NullPayload_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectVariationOrdersController(context, _mockLogger.Object);

            var result = await controller.PostVariationOrder(null!);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PutVariationOrder_ValidUpdate_UpdatesVO()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectVariationOrdersController(context, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "Office");

            var id = Guid.NewGuid();
            context.ProjectVariationOrders.Add(new ProjectVariationOrder { Id = id, Description = "Original" });
            await context.SaveChangesAsync();

            var updatePayload = new ProjectVariationOrder { Id = id, Description = "Updated Desc", Status = "Approved" };

            var result = await controller.PutVariationOrder(id, updatePayload);

            Assert.IsType<NoContentResult>(result);
            var dbVO = await context.ProjectVariationOrders.FindAsync(id);
            Assert.Equal("Updated Desc", dbVO!.Description);
        }

        [Fact]
        public async Task PutVariationOrder_MismatchId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectVariationOrdersController(context, _mockLogger.Object);

            var vo = new ProjectVariationOrder { Id = Guid.NewGuid(), Description = "Test" };
            var result = await controller.PutVariationOrder(Guid.NewGuid(), vo);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteVariationOrder_ValidId_DeletesVO()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ProjectVariationOrdersController(context, _mockLogger.Object);
            controller.ControllerContext = CreateControllerContext(Guid.NewGuid().ToString(), "Admin");

            var id = Guid.NewGuid();
            context.ProjectVariationOrders.Add(new ProjectVariationOrder { Id = id, Description = "ToDelete" });
            await context.SaveChangesAsync();

            var result = await controller.DeleteVariationOrder(id);

            Assert.IsType<NoContentResult>(result);
            var dbVO = await context.ProjectVariationOrders.FirstOrDefaultAsync(v => v.Id == id);
            Assert.Null(dbVO);
        }
    }
}
