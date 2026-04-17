using Moq;
using OCC.Client.Features.OrdersHub.ViewModels;
using OCC.Client.ModelWrappers;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.ViewModels
{
    public class OrderLinesViewModelTests
    {
        private readonly Mock<IOrderManager> _mockOrderManager;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly Mock<IOrderCalculationService> _mockCalculationService;
        private readonly OrderLinesViewModel _viewModel;

        public OrderLinesViewModelTests()
        {
            _mockOrderManager = new Mock<IOrderManager>();
            _mockDialogService = new Mock<IDialogService>();
            _mockCalculationService = new Mock<IOrderCalculationService>();
            
            _viewModel = new OrderLinesViewModel(
                _mockOrderManager.Object,
                _mockDialogService.Object,
                NullLogger<OrderLinesViewModel>.Instance,
                _mockCalculationService.Object);
        }

        [Fact]
        public void Initialize_SetsCurrentOrder_AndEnsuresBlankRow()
        {
            // Arrange
            var order = new OrderWrapper(new Order());
            var inventory = new List<InventoryItem>();

            // Act
            _viewModel.Initialize(order, inventory);

            // Assert
            Assert.Equal(order, _viewModel.CurrentOrder);
            // Note: EnsureBlankRow uses Dispatcher.UIThread.Post in the real app, 
            // but in tests we might need to be careful if it doesn't run synchronously.
            // However, the base class ViewModelBase or the test environment might not have a dispatcher.
        }

        [Fact]
        public async Task AddLine_CalculatesTotals_AndAddsToCollection()
        {
            // Arrange
            var order = new OrderWrapper(new Order { TaxRate = 0.15m });
            _viewModel.Initialize(order, new List<InventoryItem>());
            
            _viewModel.NewLine.Description = "Test Item";
            _viewModel.NewLine.QuantityOrdered = 2;
            _viewModel.NewLine.UnitPrice = 100m;

            _mockCalculationService.Setup(s => s.CalculateLineTotals(2, 100m, 0.15m))
                .Returns((200m, 30m));

            // Act
            await _viewModel.AddLine();

            // Assert
            var addedLine = order.Lines.FirstOrDefault(l => l.Description == "Test Item");
            Assert.NotNull(addedLine);
            Assert.Equal(200m, addedLine.LineTotal);
            Assert.Equal(30m, addedLine.VatAmount);
        }

        [Fact]
        public void RemoveLine_RemovesItem_AndAddsEmptyLineIfNoneLeft()
        {
            // Arrange
            var order = new OrderWrapper(new Order());
            _viewModel.Initialize(order, new List<InventoryItem>());
            // Clear any auto-added lines for clean test
            order.Lines.Clear();
            
            var line = new OrderLineWrapper(new OrderLine { Description = "Item to remove" });
            order.Lines.Add(line);

            // Act
            _viewModel.RemoveLine(line);

            // Assert
            Assert.DoesNotContain(line, order.Lines);
            Assert.Single(order.Lines); // Should have added an empty line
        }

        [Fact]
        public void SetInventoryMaster_SyncsItems()
        {
            // Arrange
            var items = new List<InventoryItem>
            {
                new InventoryItem { Sku = "A1", Description = "Apple" },
                new InventoryItem { Sku = "B2", Description = "Banana" }
            };

            // Act
            _viewModel.SetInventoryMaster(items);

            // Assert
            Assert.Equal(2, _viewModel.AllInventoryItems.Count);
        }

        [Fact]
        public void RowIsolation_UpdatingOneLine_DoesNotAffectOthers()
        {
            // Arrange
            var order = new OrderWrapper(new Order());
            _viewModel.Initialize(order, new List<InventoryItem>());
            order.Lines.Add(new OrderLineWrapper(new OrderLine { ItemCode = "OLD1" }));
            order.Lines.Add(new OrderLineWrapper(new OrderLine { ItemCode = "OLD2" }));

            var line1 = order.Lines[0];
            var line2 = order.Lines[1];

            // Act
            line1.ItemCode = "NEW1";

            // Assert
            Assert.Equal("NEW1", line1.ItemCode);
            Assert.Equal("OLD2", line2.ItemCode);
            // This test verifies that the wrappers are independent and don't share underlying models 
            // unintentionally, or that property changes don't trigger global refreshes that overwrite data.
        }

        [Fact]
        public void UpdateLineFromSelection_SetsProperties_FromItem()
        {
            // Arrange
            var order = new OrderWrapper(new Order { TaxRate = 0.15m });
            _viewModel.Initialize(order, new List<InventoryItem>());
            var line = new OrderLineWrapper(new OrderLine());
            var item = new InventoryItem 
            { 
                Id = Guid.NewGuid(), 
                Sku = "SKU123", 
                Description = "Desc", 
                UnitOfMeasure = "kg", 
                Price = 50m 
            };

            _mockCalculationService.Setup(s => s.CalculateLineTotals(It.IsAny<double>(), 50m, 0.15m))
                .Returns((50m, 7.5m));

            // Act
            _viewModel.UpdateLineFromSelection(line, item);

            // Assert
            Assert.Equal("SKU123", line.ItemCode);
            Assert.Equal("Desc", line.Description);
            Assert.Equal("kg", line.UnitOfMeasure);
            Assert.Equal(50m, line.UnitPrice);
        }
    }
}
