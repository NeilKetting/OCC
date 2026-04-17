using Moq;
using OCC.WpfClient.Features.OrdersHub.UseCases;
using OCC.WpfClient.ModelWrappers;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Managers.Interfaces;
using OCC.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.UseCases
{
    public class OrderSubmissionUseCaseTests
    {
        private readonly Mock<IOrderManager> _mockOrderManager;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly Mock<IPdfService> _mockPdfService;
        private readonly OrderSubmissionUseCase _useCase;

        public OrderSubmissionUseCaseTests()
        {
            _mockOrderManager = new Mock<IOrderManager>();
            _mockDialogService = new Mock<IDialogService>();
            _mockPdfService = new Mock<IPdfService>();

            _useCase = new OrderSubmissionUseCase(
                _mockOrderManager.Object,
                _mockDialogService.Object,
                NullLogger<OrderSubmissionUseCase>.Instance,
                _mockPdfService.Object);
        }

        [Fact]
        public async Task ExecuteAsync_Fails_IfNoLines()
        {
            // Arrange
            var order = new OrderWrapper(new Order { OrderType = OrderType.PurchaseOrder });
            order.ExpectedDeliveryDate = DateTime.Today;
            order.SupplierId = Guid.NewGuid();
            order.Lines.Clear();
            var options = new OrderSubmissionOptions(false, false, true);

            // Act
            var (success, result) = await _useCase.ExecuteAsync(order, options);

            // Assert
            Assert.False(success);
            _mockDialogService.Verify(d => d.ShowAlertAsync("Validation Error", "Please add at least one line item."), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_Fails_IfPurchaseOrder_HasNoSupplier()
        {
            // Arrange
            var order = new OrderWrapper(new Order { OrderType = OrderType.PurchaseOrder });
            order.ExpectedDeliveryDate = DateTime.Today;
            order.SupplierId = null;
            order.Lines.Add(new OrderLineWrapper(new OrderLine { Description = "Item", QuantityOrdered = 1, UnitPrice = 10, UnitOfMeasure = "ea", InventoryItemId = Guid.NewGuid() }));
            var options = new OrderSubmissionOptions(false, false, true);

            // Act
            var (success, result) = await _useCase.ExecuteAsync(order, options);

            // Assert
            Assert.False(success);
            _mockDialogService.Verify(d => d.ShowAlertAsync("Validation Error", "Please select a supplier."), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_SanitizesLines_AndSubmitsSuccessfully()
        {
            // Arrange
            var order = new OrderWrapper(new Order 
            { 
                OrderType = OrderType.PurchaseOrder,
                ExpectedDeliveryDate = DateTime.Today,
                SupplierId = Guid.NewGuid()
            });
            
            // Add one valid line and one empty/placeholder line
            order.Lines.Add(new OrderLineWrapper(new OrderLine { Description = "Valid", QuantityOrdered = 1, UnitPrice = 10, UnitOfMeasure = "ea", InventoryItemId = Guid.NewGuid() }));
            order.Lines.Add(new OrderLineWrapper(new OrderLine())); // Empty placeholder

            _mockOrderManager.Setup(m => m.CreateOrderAsync(It.IsAny<Order>()))
                .ReturnsAsync((Order o) => o);

            var options = new OrderSubmissionOptions(false, false, true);

            // Act
            var (success, result) = await _useCase.ExecuteAsync(order, options);

            // Assert
            Assert.True(success);
            Assert.NotNull(result);
            Assert.Single(result.Lines);
            _mockOrderManager.Verify(m => m.CreateOrderAsync(It.Is<Order>(o => o.Lines.Count == 1)), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_HandlesPrintOption()
        {
            // Arrange
            var order = new OrderWrapper(new Order 
            { 
                OrderType = OrderType.PurchaseOrder,
                ExpectedDeliveryDate = DateTime.Today,
                SupplierId = Guid.NewGuid()
            });
            order.Lines.Add(new OrderLineWrapper(new OrderLine { Description = "Valid", QuantityOrdered = 1, UnitPrice = 10, UnitOfMeasure = "ea", InventoryItemId = Guid.NewGuid() }));
            
            _mockOrderManager.Setup(m => m.CreateOrderAsync(It.IsAny<Order>())).ReturnsAsync((Order o) => o);
            _mockPdfService.Setup(p => p.GenerateOrderPdfAsync(It.IsAny<Order>(), true)).ReturnsAsync("path/to/pdf");

            var options = new OrderSubmissionOptions(true, false, true);

            // Act
            var (success, result) = await _useCase.ExecuteAsync(order, options);

            // Assert
            Assert.True(success);
            _mockPdfService.Verify(p => p.GenerateOrderPdfAsync(It.IsAny<Order>(), true), Times.Once);
        }
    }
}
