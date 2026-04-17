using Moq;
using OCC.Shared.Models;
using OCC.WpfClient.Features.ProcurementHub.Models;
using Xunit;
using System;
using System.Linq;

namespace OCC.Tests.Features.ProcurementHub
{
    public class OrderWrapperTests
    {
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
                {
                    totalAmountChanged = true;
                }
            };

            // Act
            lineWrapper.QuantityOrdered = 2;

            // Assert
            Assert.True(totalAmountChanged);
            Assert.Equal(200m, lineWrapper.LineTotal);
        }

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

            // Act 2
            wrapper.IsOfficeSelected = true;

            // Assert 2
            Assert.Equal(OrderDestinationType.Stock, wrapper.DestinationType);
            Assert.False(wrapper.IsSiteSelected);
            Assert.True(wrapper.IsOfficeSelected);
        }
    }
}
