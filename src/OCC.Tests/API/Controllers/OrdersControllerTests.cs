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
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            using var context = new AppDbContext(_dbOptions);
            Assert.Throws<ArgumentNullException>(() => new OrdersController(null!, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object));
            Assert.Throws<ArgumentNullException>(() => new OrdersController(context, null!, _mockHubContext.Object, _mockStockService.Object));
            Assert.Throws<ArgumentNullException>(() => new OrdersController(context, _mockLogger.Object, null!, _mockStockService.Object));
            Assert.Throws<ArgumentNullException>(() => new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, null!));
        }

        [Fact]
        public async Task GetOrders_ReturnsListOfOrderSummaryDtos()
        {
            using var context = new AppDbContext(_dbOptions);
            context.Orders.Add(new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = "PO-100",
                OrderDate = DateTime.UtcNow,
                SupplierName = "Supplier A",
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>
                {
                    new OrderLine { Id = Guid.NewGuid(), LineTotal = 100m, VatAmount = 15m }
                }
            });
            await context.SaveChangesAsync();

            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);
            var result = await controller.GetOrders();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var orders = Assert.IsType<List<OrderSummaryDto>>(okResult.Value);
            Assert.Single(orders);
            Assert.Equal("PO-100", orders.First().OrderNumber);
            Assert.Equal(115m, orders.First().TotalAmount);
        }

        [Fact]
        public async Task GetOrder_EmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var result = await controller.GetOrder(Guid.Empty);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetOrder_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var result = await controller.GetOrder(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetOrder_ValidId_ReturnsOrderDto()
        {
            using var context = new AppDbContext(_dbOptions);
            var id = Guid.NewGuid();
            context.Orders.Add(new Order
            {
                Id = id,
                OrderNumber = "PO-200",
                SupplierName = "Supplier B",
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>
                {
                    new OrderLine { Id = Guid.NewGuid(), OrderId = id, Description = "Line Item 1", QuantityOrdered = 5, UnitPrice = 10 }
                }
            });
            await context.SaveChangesAsync();

            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);
            var result = await controller.GetOrder(id);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<OrderDto>(okResult.Value);
            Assert.Equal(id, dto.Id);
            Assert.Single(dto.Lines);
        }

        [Fact]
        public async Task CreateOrder_NullPayloadOrNoLines_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var nullRes = await controller.CreateOrder(null!);
            Assert.IsType<BadRequestObjectResult>(nullRes.Result);

            var noLinesDto = new OrderDto { Lines = new List<OrderLineDto>() };
            var noLinesRes = await controller.CreateOrder(noLinesDto);
            Assert.IsType<BadRequestObjectResult>(noLinesRes.Result);
        }

        [Fact]
        public async Task CreateOrder_PastEta_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var pastEtaDto = new OrderDto
            {
                OrderNumber = "PO-PAST",
                ExpectedDeliveryDate = DateTime.Today.AddDays(-2),
                Lines = new List<OrderLineDto>
                {
                    new OrderLineDto { InventoryItemId = Guid.NewGuid(), Description = "Test", QuantityOrdered = 1, UnitPrice = 10 }
                }
            };

            var result = await controller.CreateOrder(pastEtaDto);
            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Expected delivery date (ETA) cannot be in the past.", badReq.Value);
        }

        [Fact]
        public async Task CreateOrder_DuplicateOrderNumber_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            context.Orders.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "PO-DUP" });
            await context.SaveChangesAsync();

            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);
            var dupDto = new OrderDto
            {
                OrderNumber = "PO-DUP",
                Lines = new List<OrderLineDto>
                {
                    new OrderLineDto { InventoryItemId = Guid.NewGuid(), Description = "Item", QuantityOrdered = 1, UnitPrice = 10 }
                }
            };

            var result = await controller.CreateOrder(dupDto);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateOrder_PickingOrder_AdjustsStockAndCreatesSuccessfully()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var invId = Guid.NewGuid();
            var createDto = new OrderDto
            {
                OrderNumber = "PK-001",
                OrderType = OrderType.PickingOrder,
                Branch = Branch.JHB,
                TaxRate = 0.15m,
                Lines = new List<OrderLineDto>
                {
                    new OrderLineDto
                    {
                        InventoryItemId = invId,
                        Description = "Stock Item",
                        QuantityOrdered = 10,
                        UnitPrice = 50
                    }
                }
            };

            var result = await controller.CreateOrder(createDto);
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<OrderDto>(created.Value);

            Assert.Equal("PK-001", dto.OrderNumber);
            Assert.Equal(500m, dto.Lines.First().LineTotal);
            Assert.Equal(75m, dto.Lines.First().VatAmount);

            _mockStockService.Verify(s => s.AdjustStockAsync(invId, -10, Branch.JHB), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveOrderUpdate", It.IsAny<object[]>(), default), Times.Once);
        }

        [Fact]
        public async Task UpdateOrder_AddNewLineToExistingOrder_PreservesLineIdAndSavesSuccessfully()
        {
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
            Assert.IsType<NoContentResult>(result);

            var dbOrder = await context.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId);
            Assert.NotNull(dbOrder);
            Assert.Equal(2, dbOrder.Lines.Count);
        }

        [Fact]
        public async Task UpdateOrder_RemoveLineFromExistingOrder_RemovesLineSuccessfully()
        {
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
            Assert.IsType<NoContentResult>(result);

            var dbOrder = await context.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId);
            Assert.NotNull(dbOrder);
            var activeLines = dbOrder.Lines.Where(l => l.IsActive).ToList();
            Assert.Single(activeLines);
        }

        [Fact]
        public async Task UpdateOrder_NegativeQuantityLine_ReturnsBadRequest()
        {
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

            var result = await controller.UpdateOrder(orderId, updateDto);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Quantity ordered cannot be negative.", badRequest.Value);
        }

        [Fact]
        public async Task DeleteOrder_EmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var result = await controller.DeleteOrder(Guid.Empty);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteOrder_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);

            var result = await controller.DeleteOrder(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteOrder_PickingOrder_UndoesStockAndDeletes()
        {
            using var context = new AppDbContext(_dbOptions);
            var orderId = Guid.NewGuid();
            var invId = Guid.NewGuid();

            context.Orders.Add(new Order
            {
                Id = orderId,
                OrderNumber = "PK-DEL",
                OrderType = OrderType.PickingOrder,
                Branch = Branch.CPT,
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>
                {
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderId, InventoryItemId = invId, QuantityOrdered = 5, UnitPrice = 20 }
                }
            });
            await context.SaveChangesAsync();

            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);
            var result = await controller.DeleteOrder(orderId);

            Assert.IsType<NoContentResult>(result);
            var dbOrder = await context.Orders.FindAsync(orderId);
            Assert.False(dbOrder!.IsActive);

            _mockStockService.Verify(s => s.AdjustStockAsync(invId, 5, Branch.CPT), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveOrderDelete", It.Is<object[]>(o => (Guid)o[0] == orderId), default), Times.Once);
        }

        [Fact]
        public async Task ReceiveOrder_ValidInboundPO_UpdatesReceivedQuantityAverageCostAndStatus()
        {
            using var context = new AppDbContext(_dbOptions);
            var orderId = Guid.NewGuid();
            var lineId = Guid.NewGuid();
            var invId = Guid.NewGuid();

            context.InventoryItems.Add(new InventoryItem
            {
                Id = invId,
                Sku = "CEMENT",
                Description = "Cement Bag",
                QuantityOnHand = 10,
                JhbQuantity = 10,
                AverageCost = 50m
            });

            context.Orders.Add(new Order
            {
                Id = orderId,
                OrderNumber = "PO-REC",
                OrderType = OrderType.PurchaseOrder,
                Branch = Branch.JHB,
                Status = OrderStatus.Ordered,
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>
                {
                    new OrderLine
                    {
                        Id = lineId,
                        OrderId = orderId,
                        InventoryItemId = invId,
                        QuantityOrdered = 10,
                        QuantityReceived = 0,
                        UnitPrice = 80m
                    }
                }
            });
            await context.SaveChangesAsync();

            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);
            var receivedLines = new List<OrderLineDto>
            {
                new OrderLineDto { Id = lineId, QuantityReceived = 10 }
            };

            var result = await controller.ReceiveOrder(orderId, receivedLines);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<OrderDto>(okResult.Value);

            Assert.Equal(OrderStatus.Completed, dto.Status);

            var dbInv = await context.InventoryItems.FindAsync(invId);
            Assert.NotNull(dbInv);
            Assert.Equal(20, dbInv.QuantityOnHand);
            Assert.Equal(20, dbInv.JhbQuantity);
            Assert.Equal(65m, dbInv.AverageCost);
        }

        [Fact]
        public async Task GetRestockTemplate_LowStockExists_ReturnsPrepopulatedTemplate()
        {
            using var context = new AppDbContext(_dbOptions);
            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Sku = "BRICK-01",
                Description = "Red Brick",
                Supplier = "Supplier Mega",
                TrackLowStock = true,
                JhbQuantity = 5,
                JhbReorderPoint = 10,
                AverageCost = 5m
            };
            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();

            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);
            var result = await controller.GetRestockTemplate();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var template = Assert.IsType<OrderDto>(okResult.Value);

            Assert.Equal("Supplier Mega", template.SupplierName);
            Assert.Single(template.Lines);
            Assert.Equal("BRICK-01", template.Lines.First().ItemCode);
        }

        [Fact]
        public async Task GetRestockCandidates_ReturnsLowStockItemsFilteredByBranch()
        {
            using var context = new AppDbContext(_dbOptions);
            context.InventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                Sku = "PIPE-01",
                Description = "PVC Pipe",
                TrackLowStock = true,
                JhbQuantity = 2,
                JhbReorderPoint = 5,
                CptQuantity = 10,
                CptReorderPoint = 5
            });
            await context.SaveChangesAsync();

            var controller = new OrdersController(context, _mockLogger.Object, _mockHubContext.Object, _mockStockService.Object);
            var result = await controller.GetRestockCandidates(Branch.JHB);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var candidates = Assert.IsAssignableFrom<IEnumerable<RestockCandidateDto>>(okResult.Value);

            Assert.Single(candidates);
            Assert.Equal(Branch.JHB, candidates.First().TargetBranch);
        }
    }
}
