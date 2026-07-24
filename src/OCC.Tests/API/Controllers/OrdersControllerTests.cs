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
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class OrdersControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<OrdersController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IStockService> _mockStockService;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public OrdersControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<OrdersController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockStockService = new Mock<IStockService>();

            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
        }

        [Fact]
        public async Task UpdateOrder_AddNewLineToExistingOrder_PreservesLineIdAndSavesSuccessfully()
        {
            // Arrange
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var orderId = Guid.NewGuid();
            var initialLineId = Guid.NewGuid();
            var existingOrder = new Order
            {
                Id = orderId,
                OrderNumber = "PO-TEST-001",
                SupplierName = "Test Supplier",
                OrderDate = DateTime.UtcNow,
                ExpectedDeliveryDate = DateTime.Today.AddDays(5),
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>
                {
                    new OrderLine
                    {
                        Id = initialLineId,
                        OrderId = orderId,
                        ItemCode = "ITEM-001",
                        Description = "Original Item",
                        QuantityOrdered = 2,
                        UnitPrice = 100
                    }
                }
            };

            context.Orders.Add(existingOrder);
            await context.SaveChangesAsync();

            // Act - Add a 2nd line with a client-generated Guid
            var newLineId = Guid.NewGuid();
            var updateDto = new OrderDto
            {
                Id = orderId,
                OrderNumber = "PO-TEST-001",
                SupplierName = "Test Supplier",
                ExpectedDeliveryDate = DateTime.Today.AddDays(5),
                Lines = new List<OrderLineDto>
                {
                    new OrderLineDto
                    {
                        Id = initialLineId,
                        ItemCode = "ITEM-001",
                        Description = "Original Item",
                        QuantityOrdered = 2,
                        UnitPrice = 100
                    },
                    new OrderLineDto
                    {
                        Id = newLineId,
                        ItemCode = "ITEM-002",
                        Description = "Newly Added Item",
                        QuantityOrdered = 5,
                        UnitPrice = 250
                    }
                }
            };

            var result = await controller.UpdateOrder(orderId, updateDto);

            // Assert
            Assert.IsType<NoContentResult>(result);

            var dbOrder = await context.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId);
            Assert.NotNull(dbOrder);
            Assert.Equal(2, dbOrder.Lines.Count);

            var addedLine = dbOrder.Lines.FirstOrDefault(l => l.Id == newLineId);
            Assert.NotNull(addedLine);
            Assert.Equal("ITEM-002", addedLine.ItemCode);
            Assert.Equal(5, addedLine.QuantityOrdered);
            Assert.Equal(250, addedLine.UnitPrice);
        }

        [Fact]
        public async Task UpdateOrder_RemoveLineFromExistingOrder_RemovesLineSuccessfully()
        {
            // Arrange
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var orderId = Guid.NewGuid();
            var line1Id = Guid.NewGuid();
            var line2Id = Guid.NewGuid();

            var existingOrder = new Order
            {
                Id = orderId,
                OrderNumber = "PO-TEST-002",
                SupplierName = "Supplier B",
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>
                {
                    new OrderLine { Id = line1Id, OrderId = orderId, ItemCode = "LINE-1", Description = "Line 1", QuantityOrdered = 10, UnitPrice = 50 },
                    new OrderLine { Id = line2Id, OrderId = orderId, ItemCode = "LINE-2", Description = "Line 2", QuantityOrdered = 5, UnitPrice = 30 }
                }
            };

            context.Orders.Add(existingOrder);
            await context.SaveChangesAsync();

            // Act - Send update with Line 2 removed
            var updateDto = new OrderDto
            {
                Id = orderId,
                OrderNumber = "PO-TEST-002",
                SupplierName = "Supplier B",
                Lines = new List<OrderLineDto>
                {
                    new OrderLineDto { Id = line1Id, ItemCode = "LINE-1", Description = "Line 1", QuantityOrdered = 10, UnitPrice = 50 }
                }
            };

            var result = await controller.UpdateOrder(orderId, updateDto);

            // Assert
            Assert.IsType<NoContentResult>(result);

            var dbOrder = await context.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId);
            Assert.NotNull(dbOrder);
            var activeLines = dbOrder.Lines.Where(l => l.IsActive).ToList();
            Assert.Single(activeLines);
            Assert.Equal(line1Id, activeLines.First().Id);
        }

        [Fact]
        public async Task UpdateOrder_ZeroQuantityOrZeroPriceLine_SavesSuccessfully()
        {
            // Arrange
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var orderId = Guid.NewGuid();
            var existingOrder = new Order
            {
                Id = orderId,
                OrderNumber = "PO-TEST-003",
                SupplierName = "Supplier C",
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>
                {
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderId, ItemCode = "ITEM-A", Description = "Item A", QuantityOrdered = 1, UnitPrice = 10 }
                }
            };

            context.Orders.Add(existingOrder);
            await context.SaveChangesAsync();

            var lineId = Guid.NewGuid();
            var updateDto = new OrderDto
            {
                Id = orderId,
                OrderNumber = "PO-TEST-003",
                SupplierName = "Supplier C",
                Lines = new List<OrderLineDto>
                {
                    new OrderLineDto { Id = lineId, ItemCode = "ITEM-ZERO", Description = "Unquantified / Draft Item", QuantityOrdered = 0, UnitPrice = 0 }
                }
            };

            // Act
            var result = await controller.UpdateOrder(orderId, updateDto);

            // Assert
            Assert.IsType<NoContentResult>(result);

            var dbOrder = await context.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId);
            Assert.NotNull(dbOrder);
            var line = dbOrder.Lines.FirstOrDefault(l => l.Id == lineId);
            Assert.NotNull(line);
            Assert.Equal(0, line.QuantityOrdered);
            Assert.Equal(0, line.UnitPrice);
        }

        [Fact]
        public async Task UpdateOrder_NegativeQuantityLine_ReturnsBadRequest()
        {
            // Arrange
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var orderId = Guid.NewGuid();
            var existingOrder = new Order
            {
                Id = orderId,
                OrderNumber = "PO-TEST-004",
                SupplierName = "Supplier D",
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>
                {
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderId, ItemCode = "ITEM-A", Description = "Item A", QuantityOrdered = 1, UnitPrice = 10 }
                }
            };

            context.Orders.Add(existingOrder);
            await context.SaveChangesAsync();

            var updateDto = new OrderDto
            {
                Id = orderId,
                OrderNumber = "PO-TEST-004",
                SupplierName = "Supplier D",
                Lines = new List<OrderLineDto>
                {
                    new OrderLineDto { Id = Guid.NewGuid(), ItemCode = "ITEM-NEG", Description = "Negative Qty Item", QuantityOrdered = -5, UnitPrice = 10 }
                }
            };

            // Act
            var result = await controller.UpdateOrder(orderId, updateDto);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Quantity ordered cannot be negative.", badRequest.Value);
        }

        [Fact]
        public async Task CreateOrder_ZeroQuantityLine_SavesSuccessfully()
        {
            // Arrange
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var inventoryItemId = Guid.NewGuid();
            var createDto = new OrderDto
            {
                OrderNumber = "PO-CREATE-001",
                SupplierName = "New Supplier",
                ExpectedDeliveryDate = DateTime.Today.AddDays(3),
                Lines = new List<OrderLineDto>
                {
                    new OrderLineDto
                    {
                        InventoryItemId = inventoryItemId,
                        ItemCode = "INV-001",
                        Description = "Unpriced Item",
                        QuantityOrdered = 0,
                        UnitPrice = 0
                    }
                }
            };

            // Act
            var result = await controller.CreateOrder(createDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnDto = Assert.IsType<OrderDto>(createdResult.Value);
            Assert.Single(returnDto.Lines);
            Assert.Equal(0, returnDto.Lines.First().QuantityOrdered);
            Assert.Equal(0, returnDto.Lines.First().UnitPrice);
        }
    }
}
