using Moq;
using OCC.WpfClient.Features.OrdersHub.UseCases;
using OCC.WpfClient.Features.OrdersHub.ViewModels;
using OCC.WpfClient.ModelWrappers;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Managers.Interfaces;
using OCC.Shared.Models;
using System.Security.Claims;
using Xunit;

namespace OCC.Tests.ViewModels
{
    public class CreateOrderViewModelTests
    {
        private readonly Mock<IOrderManager> _mockOrderManager;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly Mock<OrderStateService> _mockOrderStateService;
        private readonly Mock<OrderSubmissionUseCase> _mockSubmissionUseCase;
        private readonly Mock<OrderMenuViewModel> _mockOrderMenu;
        private readonly Mock<OrderLinesViewModel> _mockLines;
        private readonly Mock<InventoryLookupViewModel> _mockInventory;
        private readonly Mock<SupplierSelectorViewModel> _mockSuppliers;
        private readonly Mock<IOrderLifecycleService> _mockLifecycle;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IToastService> _mockToastService;

        private readonly CreateOrderViewModel _vm;

        public CreateOrderViewModelTests()
        {
            _mockOrderManager = new Mock<IOrderManager>();
            _mockDialogService = new Mock<IDialogService>();
            _mockAuthService = new Mock<IAuthService>();
            _mockOrderStateService = new Mock<OrderStateService>();
            _mockSubmissionUseCase = new Mock<OrderSubmissionUseCase>(null!, null!, null!, null!);
            _mockOrderMenu = new Mock<OrderMenuViewModel>();
            _mockLines = new Mock<OrderLinesViewModel>();
            _mockInventory = new Mock<InventoryLookupViewModel>();
            _mockSuppliers = new Mock<SupplierSelectorViewModel>();
            _mockLifecycle = new Mock<IOrderLifecycleService>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockToastService = new Mock<IToastService>();

            // Setup common mocks
            _mockOrderManager.Setup(m => m.CreateNewOrderTemplate(It.IsAny<OrderType>()))
                .Returns((OrderType t) => new Order 
                { 
                    OrderNumber = "NEW-123", 
                    Branch = Branch.JHB,
                    OrderType = t 
                });

            _mockAuthService.SetupGet(a => a.CurrentUser).Returns(new User { Branch = Branch.JHB });

            // IMPORTANT: Mock Lifecycle Reset to actually update the VM, otherwise CurrentOrder ignores the Manager
            _mockLifecycle.Setup(l => l.Reset(It.IsAny<CreateOrderViewModel>()))
                .Callback<CreateOrderViewModel>(vm => 
                {
                    // Simulate what the service does: Get a template and call PrepareForOrder
                    var type = vm.CurrentOrder?.OrderType ?? OrderType.PickingOrder;
                    var template = _mockOrderManager.Object.CreateNewOrderTemplate(type);
                    vm.PrepareForOrder(template, true);
                });

            // Using real VM but with mocked dependencies
            _vm = new CreateOrderViewModel(
                _mockOrderManager.Object,
                _mockDialogService.Object,
                _mockAuthService.Object,
                new Mock<Microsoft.Extensions.Logging.ILogger<CreateOrderViewModel>>().Object,
                new Mock<IPdfService>().Object,
                _mockOrderStateService.Object,
                _mockLifecycle.Object,
                _mockSubmissionUseCase.Object,
                _mockServiceProvider.Object,
                _mockToastService.Object,
                _mockOrderMenu.Object,
                _mockLines.Object,
                _mockInventory.Object,
                _mockSuppliers.Object);
        }

        [Fact]
        public void ChangeOrderType_UpdatesOrderType_AndFetchesNewTemplate()
        {
            // Arrange
            _vm.CurrentOrder.OrderType = OrderType.PurchaseOrder;

            // Act
            _vm.ChangeOrderTypeCommand.Execute(OrderType.PickingOrder);

            // Assert
            Assert.Equal(OrderType.PickingOrder, _vm.CurrentOrder.OrderType);
            // Relaxed verification because constructor also calls it
            _mockOrderManager.Verify(m => m.CreateNewOrderTemplate(OrderType.PickingOrder), Times.AtLeastOnce);
            Assert.True(_vm.IsPickingOrder);
            Assert.False(_vm.IsPurchaseOrder);
        }

        [Fact]
        public void SelectedProject_UpdatesAddress_WhenSiteDelivery()
        {
            // Arrange
            var project = new Project { Id = Guid.NewGuid(), Name = "Test Project", StreetLine1 = "123 Site St" };
            _vm.IsSiteDelivery = true;

            // Act
            _vm.SelectedProject = project;

            // Assert
            Assert.Equal(project.Id, _vm.CurrentOrder.ProjectId);
            Assert.Equal("Test Project", _vm.CurrentOrder.ProjectName);
            Assert.Equal("123 Site St", _vm.CurrentOrder.EntityAddress);
        }

        [Fact]
        public async Task SubmitOrder_CallsUseCase_AndResetsOnSuccess()
        {
            // Arrange
            _vm.CurrentOrder = new OrderWrapper(new Order { OrderNumber = "SUBMIT-TEST" });
            var orderToSubmit = _vm.CurrentOrder; // Capture before reset
            
            _mockSubmissionUseCase.Setup(u => u.ExecuteAsync(It.IsAny<OrderWrapper>(), It.IsAny<OrderSubmissionOptions>()))
                .ReturnsAsync((true, new Order()));

            // Act
            await _vm.SubmitOrderCommand.ExecuteAsync(null);

            // Assert
            // Verify using the captured instance, as CurrentOrder is replaced by Reset()
            _mockSubmissionUseCase.Verify(u => u.ExecuteAsync(orderToSubmit, It.IsAny<OrderSubmissionOptions>()), Times.Once);
            
            // Reset logic
            Assert.Null(_vm.CurrentOrder.ProjectId); 
            _mockLifecycle.Verify(l => l.Reset(_vm), Times.AtLeastOnce); 
        }

        [Fact]
        public async Task PreviewOrder_ShowsAlert_IfNoSupplierSelected_ForPurchaseOrder()
        {
            // Arrange
            // Use command to ensure IsPurchaseOrder flag is updated
            _vm.ChangeOrderTypeCommand.Execute(OrderType.PurchaseOrder);
            _vm.Suppliers.SelectedSupplier = null; // Ensure null

            // Act
            await _vm.PreviewOrderCommand.ExecuteAsync(null);

            // Assert
            _mockDialogService.Verify(d => d.ShowAlertAsync("Validation", "Please select a supplier first."), Times.Once);
        }
    }
}
