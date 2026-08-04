using OCC.Shared.Models;
using OCC.WpfClient.Features.ProcurementHub.Models;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OCC.Tests.Features.ProcurementHub
{
    /// <summary>
    /// Unit tests for <see cref="OrderWrapper"/> and <see cref="OrderLineWrapper"/>.
    /// </summary>
    public class OrderWrapperTests
    {
        // ─── Construction Tests ───────────────────────────────────────────────────

        /// <summary>
        /// Regression test for the double-add bug.
        /// When wrapping an Order that already has N lines, the wrapper collection must
        /// contain exactly N lines and the Model.Lines must still contain exactly N lines —
        /// not 2N (which happened before the _suppressModelSync fix).
        /// </summary>
        [Fact]
        public void Constructor_ExistingLines_DoesNotDoubleAddToModelLines()
        {
            // Arrange — order with 3 pre-existing lines (simulates loading from API)
            var orderId = Guid.NewGuid();
            var order = new Order
            {
                Id = orderId,
                TaxRate = 0.15m,
                Lines = new List<OrderLine>
                {
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderId, ItemCode = "SKU-001", Description = "Line 1", QuantityOrdered = 1, UnitPrice = 100 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderId, ItemCode = "SKU-002", Description = "Line 2", QuantityOrdered = 2, UnitPrice = 200 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderId, ItemCode = "SKU-003", Description = "Line 3", QuantityOrdered = 3, UnitPrice = 300 }
                }
            };

            // Act
            var wrapper = new OrderWrapper(order);

            // Assert — wrapper has 3 lines
            Assert.Equal(3, wrapper.Lines.Count);

            // Assert — Model.Lines still has exactly 3 lines (not 6 due to double-add)
            Assert.Equal(3, wrapper.Model.Lines.Count);
        }

        /// <summary>
        /// Verifies that AddLine (via the wrapper) adds the line to both the observable
        /// collection and the underlying model exactly once.
        /// </summary>
        [Fact]
        public void AddLine_AddsToWrapperAndModelExactlyOnce()
        {
            // Arrange
            var order = new Order { Id = Guid.NewGuid(), TaxRate = 0.15m };
            var wrapper = new OrderWrapper(order);

            var newLine = new OrderLine { Id = Guid.NewGuid(), OrderId = order.Id };
            var lineWrapper = new OrderLineWrapper(newLine, wrapper);

            // Act
            wrapper.Lines.Add(lineWrapper);

            // Assert
            Assert.Single(wrapper.Lines);
            Assert.Single(wrapper.Model.Lines);
            Assert.Same(newLine, wrapper.Model.Lines.First());
        }

        /// <summary>
        /// Verifies that removing a line from the wrapper also removes it from the model.
        /// </summary>
        [Fact]
        public void RemoveLine_RemovesFromWrapperAndModel()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var lineModel = new OrderLine { Id = Guid.NewGuid(), OrderId = orderId, ItemCode = "SKU-X", Description = "Test" };
            var order = new Order
            {
                Id = orderId,
                TaxRate = 0.15m,
                Lines = new List<OrderLine> { lineModel }
            };
            var wrapper = new OrderWrapper(order);

            Assert.Single(wrapper.Lines);
            Assert.Single(wrapper.Model.Lines);

            // Act
            wrapper.Lines.Remove(wrapper.Lines.First());

            // Assert
            Assert.Empty(wrapper.Lines);
            Assert.Empty(wrapper.Model.Lines);
        }

        // ─── Calculation Tests ────────────────────────────────────────────────────

        [Fact]
        public void OrderWrapper_Calculation_UpdatesWhenLineAdded()
        {
            // Arrange
            var order = new Order { Id = Guid.NewGuid(), TaxRate = 0.15m };
            var wrapper = new OrderWrapper(order);
            var line = new OrderLine { Id = Guid.NewGuid(), QuantityOrdered = 10, UnitPrice = 100 };
            var lineWrapper = new OrderLineWrapper(line, wrapper);

            // Act
            wrapper.Lines.Add(lineWrapper);
            lineWrapper.UpdateCalculations();

            // Assert
            Assert.Equal(1000m, wrapper.SubTotal);
            Assert.Equal(150m, wrapper.VatTotal);
            Assert.Equal(1150m, wrapper.TotalAmount);
        }

        [Fact]
        public void OrderWrapper_Calculation_UpdatesWhenLineQuantityChanged()
        {
            // Arrange
            var order = new Order { Id = Guid.NewGuid(), TaxRate = 0.15m };
            var wrapper = new OrderWrapper(order);
            var line = new OrderLine { Id = Guid.NewGuid(), QuantityOrdered = 1, UnitPrice = 100 };
            var lineWrapper = new OrderLineWrapper(line, wrapper);
            wrapper.Lines.Add(lineWrapper);
            lineWrapper.UpdateCalculations();

            // Act
            lineWrapper.QuantityOrdered = 5;

            // Assert
            Assert.Equal(500m, wrapper.SubTotal);
            Assert.Equal(75m, wrapper.VatTotal);
            Assert.Equal(575m, wrapper.TotalAmount);
        }

        [Fact]
        public void OrderLineWrapper_UpdateCalculations_NotifiesParent()
        {
            // Arrange
            var order = new Order { Id = Guid.NewGuid(), TaxRate = 0.15m };
            var wrapper = new OrderWrapper(order);
            var line = new OrderLine { Id = Guid.NewGuid(), QuantityOrdered = 1, UnitPrice = 100 };
            var lineWrapper = new OrderLineWrapper(line, wrapper);
            wrapper.Lines.Add(lineWrapper);

            bool totalAmountChanged = false;
            wrapper.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(wrapper.TotalAmount))
                    totalAmountChanged = true;
            };

            // Act
            lineWrapper.QuantityOrdered = 2;

            // Assert
            Assert.True(totalAmountChanged);
            Assert.Equal(200m, lineWrapper.LineTotal);
        }

        // ─── DestinationType Toggle Tests ─────────────────────────────────────────

        [Fact]
        public void DestinationType_Toggles_UpdateCorrectly()
        {
            // Arrange
            var order = new Order { DestinationType = OrderDestinationType.Stock };
            var wrapper = new OrderWrapper(order);

            // Act
            wrapper.IsSiteSelected = true;

            // Assert
            Assert.Equal(OrderDestinationType.Site, wrapper.DestinationType);
            Assert.True(wrapper.IsSiteSelected);
            Assert.False(wrapper.IsOfficeSelected);
            Assert.False(wrapper.IsOtherSelected);

            // Act 2
            wrapper.IsOfficeSelected = true;

            // Assert 2
            Assert.Equal(OrderDestinationType.Stock, wrapper.DestinationType);
            Assert.False(wrapper.IsSiteSelected);
            Assert.True(wrapper.IsOfficeSelected);
            Assert.False(wrapper.IsOtherSelected);
        }

        // ─── Branch Property Test ─────────────────────────────────────────────────

        [Fact]
        public void Branch_Wrapper_ReadsAndWritesModelCorrectly()
        {
            // Arrange
            var order = new Order { Branch = Branch.JHB };
            var wrapper = new OrderWrapper(order);

            // Act
            wrapper.Branch = Branch.CPT;

            // Assert
            Assert.Equal(Branch.CPT, wrapper.Branch);
            Assert.Equal(Branch.CPT, order.Branch);
        }

        // ─── IsItemValid Tests ────────────────────────────────────────────────────

        [Fact]
        public void IsItemValid_FalseWhenItemCodeMissingEvenWithId()
        {
            var order = new Order { Id = Guid.NewGuid() };
            var wrapper = new OrderWrapper(order);
            var line = new OrderLine { Id = Guid.NewGuid(), InventoryItemId = Guid.NewGuid() };
            var lineWrapper = new OrderLineWrapper(line, wrapper);

            // ItemCode is empty — IsItemValid must be false
            Assert.False(lineWrapper.IsItemValid);
        }

        [Fact]
        public void IsItemValid_FalseWhenInventoryItemIdMissing()
        {
            var order = new Order { Id = Guid.NewGuid() };
            var wrapper = new OrderWrapper(order);
            var line = new OrderLine { Id = Guid.NewGuid(), ItemCode = "SKU-001" };
            var lineWrapper = new OrderLineWrapper(line, wrapper);

            // InventoryItemId is null — IsItemValid must be false
            Assert.False(lineWrapper.IsItemValid);
        }

        [Fact]
        public void IsItemValid_TrueWhenBothCodeAndIdSet()
        {
            var order = new Order { Id = Guid.NewGuid() };
            var wrapper = new OrderWrapper(order);
            var line = new OrderLine { Id = Guid.NewGuid(), ItemCode = "SKU-001", InventoryItemId = Guid.NewGuid() };
            var lineWrapper = new OrderLineWrapper(line, wrapper);

            Assert.True(lineWrapper.IsItemValid);
        }
    }
}
