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
    public class InventoryControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<InventoryController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public InventoryControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<InventoryController>>();
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
            Assert.Throws<ArgumentNullException>(() => new InventoryController(null!, _mockLogger.Object, _mockHubContext.Object));
            Assert.Throws<ArgumentNullException>(() => new InventoryController(context, null!, _mockHubContext.Object));
            Assert.Throws<ArgumentNullException>(() => new InventoryController(context, _mockLogger.Object, null!));
        }

        [Fact]
        public async Task GetInventorySummaries_ReturnsOkResultWithSummaries()
        {
            using var context = new AppDbContext(_dbOptions);
            context.InventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                Sku = "SKU-1",
                Description = "Item A",
                Price = 12.3456m,
                JhbQuantity = 10,
                CptQuantity = 5,
                QuantityOnHand = 15
            });
            await context.SaveChangesAsync();

            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);
            var result = await controller.GetInventorySummaries();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var summaries = Assert.IsAssignableFrom<IEnumerable<InventorySummaryDto>>(okResult.Value);
            Assert.Single(summaries);
            Assert.Equal(12.35m, summaries.First().Price);
        }

        [Fact]
        public async Task GetInventory_ReturnsAllItems()
        {
            using var context = new AppDbContext(_dbOptions);
            context.InventoryItems.Add(new InventoryItem { Id = Guid.NewGuid(), Sku = "SKU-1", Description = "Item 1" });
            context.InventoryItems.Add(new InventoryItem { Id = Guid.NewGuid(), Sku = "SKU-2", Description = "Item 2" });
            await context.SaveChangesAsync();

            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);
            var result = await controller.GetInventory();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var items = Assert.IsAssignableFrom<List<InventoryItem>>(okResult.Value);
            Assert.Equal(2, items.Count);
        }

        [Fact]
        public async Task GetInventoryItem_EmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.GetInventoryItem(Guid.Empty);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetInventoryItem_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.GetInventoryItem(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetInventoryItem_ValidId_ReturnsItem()
        {
            using var context = new AppDbContext(_dbOptions);
            var id = Guid.NewGuid();
            context.InventoryItems.Add(new InventoryItem { Id = id, Sku = "TEST", Description = "Test Item" });
            await context.SaveChangesAsync();

            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);
            var result = await controller.GetInventoryItem(id);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var item = Assert.IsType<InventoryItem>(okResult.Value);
            Assert.Equal(id, item.Id);
        }

        [Fact]
        public async Task CreateItem_NullPayload_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.CreateItem(null!);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateItem_EmptyDescription_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);

            var item = new InventoryItem { Description = "   ", Sku = "SKU-X" };
            var result = await controller.CreateItem(item);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Inventory item description is required.", badReq.Value);
        }

        [Fact]
        public async Task CreateItem_NegativePriceOrCost_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);

            var itemNegPrice = new InventoryItem { Description = "Item", Price = -10m };
            var res1 = await controller.CreateItem(itemNegPrice);
            Assert.IsType<BadRequestObjectResult>(res1.Result);

            var itemNegCost = new InventoryItem { Description = "Item", AverageCost = -5m };
            var res2 = await controller.CreateItem(itemNegCost);
            Assert.IsType<BadRequestObjectResult>(res2.Result);
        }

        [Fact]
        public async Task CreateItem_DuplicateSku_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            context.InventoryItems.Add(new InventoryItem { Id = Guid.NewGuid(), Sku = "EXISTING-SKU", Description = "Existing" });
            await context.SaveChangesAsync();

            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);
            var newItem = new InventoryItem { Sku = "existing-sku", Description = "New Item" };

            var result = await controller.CreateItem(newItem);
            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("already exists", badReq.Value?.ToString());
        }

        [Fact]
        public async Task CreateItem_ValidItem_CreatesAndEmitsSignalR()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);

            var item = new InventoryItem
            {
                Sku = "NEW-SKU",
                Description = "Fresh Cement",
                JhbQuantity = 20,
                CptQuantity = 30,
                Price = 150.856m,
                AverageCost = 100.444m
            };

            var result = await controller.CreateItem(item);
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnedItem = Assert.IsType<InventoryItem>(created.Value);

            Assert.NotEqual(Guid.Empty, returnedItem.Id);
            Assert.Equal(50, returnedItem.QuantityOnHand);
            Assert.Equal(150.86m, returnedItem.Price);
            Assert.Equal(100.44m, returnedItem.AverageCost);

            _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveInventoryUpdate", It.Is<object[]>(o => o[0].ToString() == "ItemCreated"), default), Times.Once);
        }

        [Fact]
        public async Task UpdateItem_NullOrMismatchId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);

            var item = new InventoryItem { Id = Guid.NewGuid(), Description = "Test" };
            var result = await controller.UpdateItem(Guid.NewGuid(), item);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateItem_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);

            var id = Guid.NewGuid();
            var item = new InventoryItem { Id = id, Description = "Test Item" };

            var result = await controller.UpdateItem(id, item);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdateItem_ValidUpdate_UpdatesAndReturnsNoContent()
        {
            using var context = new AppDbContext(_dbOptions);
            var id = Guid.NewGuid();
            context.InventoryItems.Add(new InventoryItem { Id = id, Sku = "SKU-UP", Description = "Old Desc", JhbQuantity = 5, CptQuantity = 5, QuantityOnHand = 10 });
            await context.SaveChangesAsync();

            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);
            var updatePayload = new InventoryItem { Id = id, Sku = "SKU-UP", Description = "New Desc", JhbQuantity = 15, CptQuantity = 10, Price = 99.999m };

            var result = await controller.UpdateItem(id, updatePayload);
            Assert.IsType<NoContentResult>(result);

            var dbItem = await context.InventoryItems.FindAsync(id);
            Assert.NotNull(dbItem);
            Assert.Equal("New Desc", dbItem.Description);
            Assert.Equal(25, dbItem.QuantityOnHand);
            Assert.Equal(100.00m, dbItem.Price);
        }

        [Fact]
        public async Task DeleteInventoryItem_EmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.DeleteInventoryItem(Guid.Empty);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteInventoryItem_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.DeleteInventoryItem(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteInventoryItem_ItemUsedInOrders_ReturnsConflict()
        {
            using var context = new AppDbContext(_dbOptions);
            var itemId = Guid.NewGuid();
            context.InventoryItems.Add(new InventoryItem { Id = itemId, Sku = "USED", Description = "Used Item" });
            context.OrderLines.Add(new OrderLine { Id = Guid.NewGuid(), InventoryItemId = itemId, Description = "Line" });
            await context.SaveChangesAsync();

            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);
            var result = await controller.DeleteInventoryItem(itemId);

            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("used in existing orders", conflictResult.Value?.ToString());
        }

        [Fact]
        public async Task DeleteInventoryItem_UnusedItem_DeletesSuccessfully()
        {
            using var context = new AppDbContext(_dbOptions);
            var itemId = Guid.NewGuid();
            context.InventoryItems.Add(new InventoryItem { Id = itemId, Sku = "UNUSED", Description = "Unused Item" });
            await context.SaveChangesAsync();

            var controller = new InventoryController(context, _mockLogger.Object, _mockHubContext.Object);
            var result = await controller.DeleteInventoryItem(itemId);

            Assert.IsType<NoContentResult>(result);
            var dbItem = await context.InventoryItems.FindAsync(itemId);
            Assert.False(dbItem!.IsActive);
        }
    }
}
