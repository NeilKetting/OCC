using Moq;
using OCC.WpfClient.Features.OrdersHub.ViewModels;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Managers.Interfaces;
using OCC.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.ViewModels
{
    public class InventoryLookupViewModelTests
    {
        private readonly Mock<IOrderManager> _mockOrderManager;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly InventoryLookupViewModel _viewModel;

        public InventoryLookupViewModelTests()
        {
            _mockOrderManager = new Mock<IOrderManager>();
            _mockDialogService = new Mock<IDialogService>();
            
            _viewModel = new InventoryLookupViewModel(
                _mockOrderManager.Object,
                _mockDialogService.Object,
                NullLogger<InventoryLookupViewModel>.Instance);
        }

        [Fact]
        public void Initialize_PopulatesCategories()
        {
            // Arrange
            var inventory = new List<InventoryItem>
            {
                new InventoryItem { Category = "Tools" },
                new InventoryItem { Category = "Materials" }
            };

            // Act
            _viewModel.Initialize(inventory);

            // Assert
            Assert.Contains("Tools", _viewModel.Categories);
            Assert.Contains("Materials", _viewModel.Categories);
            Assert.Contains("General", _viewModel.Categories);
        }

        [Fact]
        public void Filter_WeightedSearch_OrdersCorrectly()
        {
            // Arrange
            var inventory = new List<InventoryItem>
            {
                new InventoryItem { Sku = "BEER", Description = "Cold Beer" },
                new InventoryItem { Sku = "B123", Description = "Beer Bottle" },
                new InventoryItem { Sku = "C1", Description = "Cheese and Beer" }
            };
            _viewModel.Initialize(inventory);

            // Act
            _viewModel.SearchText = "Beer";
            _viewModel.Filter();

            // Assert
            // SKU exact match (BEER) should be first
            Assert.Equal("BEER", _viewModel.FilteredItems[0].Sku);
            // Description starts with (Beer Bottle) should be before "Cheese and Beer"
            Assert.Equal("B123", _viewModel.FilteredItems[1].Sku);
        }

        // QuickCreateProduct logic has been moved to ItemDetailViewModel
        // Removing the outdated test to fix compilation
    }
}
