using Microsoft.Extensions.Logging;
using Moq;
using OCC.Shared.Interfaces;
using OCC.Shared.Models;
using OCC.WpfClient.Features.ProcurementHub.ViewModels;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Features.ProcurementHub
{
    public class PurchaseOrderDetailViewModelTests
    {
        private readonly Mock<IOrderService> _mockOrderService;
        private readonly Mock<ISupplierService> _mockSupplierService;
        private readonly Mock<IProjectService> _mockProjectService;
        private readonly Mock<IInventoryService> _mockInventoryService;
        private readonly Mock<INavigationService> _mockNavigationService;
        private readonly Mock<IPdfService> _mockPdfService;
        private readonly Mock<IToastService> _mockToastService;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly Mock<IGoogleMapsService> _mockGoogleMapsService;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly Mock<ILogger<PurchaseOrderDetailViewModel>> _mockLogger;

        public PurchaseOrderDetailViewModelTests()
        {
            _mockOrderService = new Mock<IOrderService>();
            _mockSupplierService = new Mock<ISupplierService>();
            _mockProjectService = new Mock<IProjectService>();
            _mockInventoryService = new Mock<IInventoryService>();
            _mockNavigationService = new Mock<INavigationService>();
            _mockPdfService = new Mock<IPdfService>();
            _mockToastService = new Mock<IToastService>();
            _mockDialogService = new Mock<IDialogService>();
            _mockGoogleMapsService = new Mock<IGoogleMapsService>();
            _mockSettingsService = new Mock<ISettingsService>();
            _mockAuthService = new Mock<IAuthService>();
            _mockLogger = new Mock<ILogger<PurchaseOrderDetailViewModel>>();

            _mockAuthService.SetupGet(a => a.CurrentUser).Returns(new User { Branch = Branch.JHB });

            _mockSupplierService.Setup(s => s.GetSuppliersAsync()).ReturnsAsync(new List<Supplier>());
            _mockProjectService.Setup(p => p.GetProjectsAsync()).ReturnsAsync(new List<Project>());
            _mockInventoryService.Setup(i => i.GetInventoryAsync()).ReturnsAsync(new List<InventoryItem>());
        }

        private PurchaseOrderDetailViewModel CreateViewModel()
        {
            return new PurchaseOrderDetailViewModel(
                _mockOrderService.Object,
                _mockSupplierService.Object,
                _mockProjectService.Object,
                _mockInventoryService.Object,
                _mockNavigationService.Object,
                _mockPdfService.Object,
                _mockToastService.Object,
                _mockDialogService.Object,
                _mockGoogleMapsService.Object,
                _mockSettingsService.Object,
                _mockAuthService.Object,
                new ConnectionSettings(),
                _mockLogger.Object);
        }

        [Fact]
        public async Task OpenExistingOrder_AddLine_And_SaveOrderAsync_CallsUpdateOrderWithAddedLine()
        {
            // Arrange
            var vm = CreateViewModel();

            var orderId = Guid.NewGuid();
            var existingOrder = new Order
            {
                Id = orderId,
                OrderNumber = "PO-EXISTING-100",
                SupplierId = Guid.NewGuid(),
                SupplierName = "Existing Supplier",
                OrderType = OrderType.PurchaseOrder,
                ExpectedDeliveryDate = DateTime.Today.AddDays(7),
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>
                {
                    new OrderLine
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderId,
                        ItemCode = "EX-001",
                        Description = "Existing Line Item",
                        QuantityOrdered = 10,
                        UnitPrice = 50
                    }
                }
            };

            _mockOrderService.Setup(o => o.GetOrderAsync(orderId)).ReturnsAsync(existingOrder);
            _mockOrderService.Setup(o => o.UpdateOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

            // Act 1 - Set OrderId and Load existing order
            vm.OrderId = orderId;
            await vm.LoadDataCommand.ExecuteAsync(null);

            Assert.False(vm.IsNewOrder);
            Assert.Equal(orderId, vm.CurrentOrder.Id);
            Assert.Single(vm.CurrentOrder.Lines);

            // Act 2 - User clicks "+ Add Line" and enters details for line 2
            vm.AddLineCommand.Execute(null);
            Assert.Equal(2, vm.CurrentOrder.Lines.Count);

            var newLine = vm.CurrentOrder.Lines.Last();
            newLine.ItemCode = "NEW-LINE-002";
            newLine.Description = "Newly Added Line Item";
            newLine.QuantityOrdered = 5;
            newLine.UnitPrice = 200;

            // Act 3 - User clicks "Save & Close"
            await vm.SaveOrderCommand.ExecuteAsync(null);

            // Assert
            _mockOrderService.Verify(s => s.UpdateOrderAsync(It.Is<Order>(o =>
                o.Id == orderId &&
                o.Lines.Count == 2 &&
                o.Lines.Any(l => l.ItemCode == "EX-001" && l.QuantityOrdered == 10) &&
                o.Lines.Any(l => l.ItemCode == "NEW-LINE-002" && l.QuantityOrdered == 5 && l.UnitPrice == 200)
            )), Times.Once);

            _mockToastService.Verify(t => t.ShowSuccess("Order Saved", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task OpenExistingOrder_AddZeroQuantityLine_And_SaveOrderAsync_PreservesZeroQuantityLine()
        {
            // Arrange
            var vm = CreateViewModel();

            var orderId = Guid.NewGuid();
            var existingOrder = new Order
            {
                Id = orderId,
                OrderNumber = "PO-EXISTING-200",
                SupplierId = Guid.NewGuid(),
                SupplierName = "Supplier 200",
                OrderType = OrderType.PurchaseOrder,
                ExpectedDeliveryDate = DateTime.Today.AddDays(7),
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>
                {
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderId, ItemCode = "LINE-1", Description = "Existing Line", QuantityOrdered = 2, UnitPrice = 100 }
                }
            };

            _mockOrderService.Setup(o => o.GetOrderAsync(orderId)).ReturnsAsync(existingOrder);
            _mockOrderService.Setup(o => o.UpdateOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

            vm.OrderId = orderId;
            await vm.LoadDataCommand.ExecuteAsync(null);

            // Act - Add unpriced/unquantified line
            vm.AddLineCommand.Execute(null);
            var newLine = vm.CurrentOrder.Lines.Last();
            newLine.ItemCode = "UNPRICED-001";
            newLine.Description = "Draft Unpriced Item";
            newLine.QuantityOrdered = 0;
            newLine.UnitPrice = 0;

            await vm.SaveOrderCommand.ExecuteAsync(null);

            // Assert
            _mockOrderService.Verify(s => s.UpdateOrderAsync(It.Is<Order>(o =>
                o.Lines.Count == 2 &&
                o.Lines.Any(l => l.ItemCode == "UNPRICED-001" && l.QuantityOrdered == 0 && l.UnitPrice == 0)
            )), Times.Once);
        }
    }
}
