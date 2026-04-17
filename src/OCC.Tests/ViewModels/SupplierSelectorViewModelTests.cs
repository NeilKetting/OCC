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
    public class SupplierSelectorViewModelTests
    {
        private readonly Mock<IOrderManager> _mockOrderManager;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly SupplierSelectorViewModel _viewModel;

        public SupplierSelectorViewModelTests()
        {
            _mockOrderManager = new Mock<IOrderManager>();
            _mockDialogService = new Mock<IDialogService>();
            
            _viewModel = new SupplierSelectorViewModel(
                _mockOrderManager.Object,
                _mockDialogService.Object,
                NullLogger<SupplierSelectorViewModel>.Instance);
        }

        [Fact]
        public void Initialize_FiltersSuppliers_ByBranch()
        {
            // Arrange
            var suppliers = new List<Supplier>
            {
                new Supplier { Name = "Global", Branch = null },
                new Supplier { Name = "CPT Only", Branch = Branch.CPT },
                new Supplier { Name = "JHB Only", Branch = Branch.JHB }
            };

            // Act
            _viewModel.Initialize(suppliers, Branch.CPT);

            // Assert
            Assert.Equal(2, _viewModel.FilteredSuppliers.Count);
            Assert.Contains(_viewModel.FilteredSuppliers, s => s.Name == "Global");
            Assert.Contains(_viewModel.FilteredSuppliers, s => s.Name == "CPT Only");
            Assert.DoesNotContain(_viewModel.FilteredSuppliers, s => s.Name == "JHB Only");
        }

        [Fact]
        public void Filter_RetainsSelection_IfValid()
        {
            // Arrange
            var global = new Supplier { Name = "Global", Branch = null };
            var suppliers = new List<Supplier> { global };
            _viewModel.Initialize(suppliers, Branch.CPT);
            _viewModel.SelectedSupplier = global;

            // Act
            _viewModel.Filter(Branch.JHB);

            // Assert
            Assert.Equal(global, _viewModel.SelectedSupplier);
        }

        [Fact]
        public async Task QuickCreateSupplier_AddsToCollections_AndSetsSelection()
        {
            // Arrange
            _viewModel.NewSupplierName = "New Guy";
            var created = new Supplier { Name = "New Guy" };
            _mockOrderManager.Setup(m => m.QuickCreateSupplierAsync("New Guy")).ReturnsAsync(created);

            // Act
            await _viewModel.QuickCreateSupplier();

            // Assert
            Assert.Contains(created, _viewModel.FilteredSuppliers);
            Assert.Equal(created, _viewModel.SelectedSupplier);
            Assert.False(_viewModel.IsAddingNew);
        }
    }
}
