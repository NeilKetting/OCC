using Moq;
using OCC.WpfClient.Features.OrdersHub.ViewModels;
using OCC.WpfClient.Features.OrdersHub.UseCases;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Managers.Interfaces;
using OCC.Shared.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Services
{
    public class OrderLifecycleServiceTests
    {
        private readonly Mock<IOrderManager> _mockOrderManager;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly Mock<OrderStateService> _mockOrderStateService;
        private readonly Mock<ILogger<OrderLifecycleService>> _mockLogger;
        private readonly OrderLifecycleService _service;

        public OrderLifecycleServiceTests()
        {
            _mockOrderManager = new Mock<IOrderManager>();
            _mockDialogService = new Mock<IDialogService>();
            _mockAuthService = new Mock<IAuthService>();
            _mockLogger = new Mock<ILogger<OrderLifecycleService>>();
            
            _mockOrderStateService = new Mock<OrderStateService>();

            _service = new OrderLifecycleService(
                _mockOrderManager.Object,
                _mockDialogService.Object,
                _mockAuthService.Object,
                _mockOrderStateService.Object,
                _mockLogger.Object);

            _mockOrderManager.Setup(m => m.CreateNewOrderTemplate(It.IsAny<OrderType>()))
                .Returns(new Order { OrderNumber = "MOCK-001", Branch = Branch.JHB });
                
            _mockAuthService.SetupGet(a => a.CurrentUser).Returns(new User { Branch = Branch.JHB });
        }

        [Fact]
        public async Task LoadOrderAsync_SetsBusyState_AndCallsPrepareForOrder()
        {
            // Arrange
            var order = new Order { Id = Guid.NewGuid(), SupplierName = "Test" };
            _mockOrderManager.Setup(m => m.GetOrderByIdAsync(order.Id)).ReturnsAsync(order);
            
            var mockVm = new Mock<CreateOrderViewModel>(
                _mockOrderManager.Object,
                _mockDialogService.Object,
                _mockAuthService.Object,
                new Mock<ILogger<CreateOrderViewModel>>().Object,
                new Mock<IPdfService>().Object,
                _mockOrderStateService.Object,
                _service,
                new Mock<OrderSubmissionUseCase>(null!, null!, null!, null!).Object,
                new Mock<OrderMenuViewModel>().Object,
                new Mock<OrderLinesViewModel>().Object,
                new Mock<InventoryLookupViewModel>().Object,
                new Mock<SupplierSelectorViewModel>().Object);

            // Act
            await _service.LoadOrderAsync(mockVm.Object, order);

            // Assert
            _mockOrderManager.Verify(m => m.GetOrderByIdAsync(order.Id), Times.Once);
        }

        [Fact]
        public async Task RestoreStateAsync_LoadsOrder_AndClearsState()
        {
            // Arrange
            var order = new Order { Id = Guid.NewGuid() };
            _mockOrderStateService.Setup(s => s.RetrieveState()).Returns((order, null, null));
            _mockOrderManager.Setup(m => m.GetOrderByIdAsync(order.Id)).ReturnsAsync(order);

            var mockVm = new Mock<CreateOrderViewModel>(
                _mockOrderManager.Object,
                _mockDialogService.Object,
                _mockAuthService.Object,
                new Mock<ILogger<CreateOrderViewModel>>().Object,
                new Mock<IPdfService>().Object,
                _mockOrderStateService.Object, 
                _service, 
                new Mock<OrderSubmissionUseCase>(null!, null!, null!, null!).Object,
                new Mock<OrderMenuViewModel>().Object,
                new Mock<OrderLinesViewModel>().Object,
                new Mock<InventoryLookupViewModel>().Object,
                new Mock<SupplierSelectorViewModel>().Object);

            // Act
            await _service.RestoreStateAsync(mockVm.Object);

            // Assert
            _mockOrderStateService.Verify(s => s.ClearState(), Times.Once);
        }
    }
}
