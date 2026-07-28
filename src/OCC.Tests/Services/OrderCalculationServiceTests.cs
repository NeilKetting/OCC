using OCC.Client.Services;
using System.Collections.Generic;
using Xunit;

namespace OCC.Tests.Services
{
    public class OrderCalculationServiceTests
    {
        private readonly OrderCalculationService _service;

        public OrderCalculationServiceTests()
        {
            _service = new OrderCalculationService();
        }

        [Theory]
        [InlineData(10, 50.00, 0.15, 500.00, 75.00)]
        [InlineData(1, 19.99, 0.15, 19.99, 3.00)] // 19.99 * 0.15 = 2.9985 -> 3.00
        [InlineData(2.5, 100.00, 0.15, 250.00, 37.50)]
        public void CalculateLineTotals_ReturnsCorrectValues(double qty, decimal price, decimal tax, decimal expectedNet, decimal expectedVat)
        {
            var (net, vat) = _service.CalculateLineTotals(qty, price, tax);

            Assert.Equal(expectedNet, net);
            Assert.Equal(expectedVat, vat);
        }

        [Fact]
        public void CalculateLineTotals_NegativeInputs_ClampsToZero()
        {
            var (net, vat) = _service.CalculateLineTotals(-5, -100, -0.15m);

            Assert.Equal(0m, net);
            Assert.Equal(0m, vat);
        }

        [Fact]
        public void CalculateOrderTotals_ReturnsSumOfLines()
        {
            var lines = new List<(decimal Net, decimal Vat)>
            {
                (100.00m, 15.00m),
                (200.00m, 30.00m)
            };

            var (sub, vat, total) = _service.CalculateOrderTotals(lines);

            Assert.Equal(300.00m, sub);
            Assert.Equal(45.00m, vat);
            Assert.Equal(345.00m, total);
        }

        [Fact]
        public void CalculateOrderTotals_NullLines_ReturnsZero()
        {
            var (sub, vat, total) = _service.CalculateOrderTotals(null!);

            Assert.Equal(0m, sub);
            Assert.Equal(0m, vat);
            Assert.Equal(0m, total);
        }
    }
}
