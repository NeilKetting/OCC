using System;
using System.Globalization;
using System.Windows.Media;
using OCC.WpfClient.Infrastructure.Converters;
using Xunit;

namespace OCC.Tests.Infrastructure
{
    public class ExpiryColorConverterTests
    {
        [Fact]
        public void Convert_NullDate_ReturnsMutedBrush()
        {
            var converter = new ExpiryColorConverter();
            var result = converter.Convert(null, typeof(Brush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

            Assert.NotNull(result);
            Assert.Equal(ColorConverter.ConvertFromString("#475569"), result.Color);
        }

        [Fact]
        public void Convert_ExpiredDate_ReturnsRedBrush()
        {
            var converter = new ExpiryColorConverter();
            var expiredDate = DateTime.Today.AddDays(-5);
            var result = converter.Convert(expiredDate, typeof(Brush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

            Assert.NotNull(result);
            Assert.Equal(ColorConverter.ConvertFromString("#991B1B"), result.Color);
        }

        [Fact]
        public void Convert_ExpiringSoonDate_ReturnsAmberBrush()
        {
            var converter = new ExpiryColorConverter();
            var expiringDate = DateTime.Today.AddDays(15);
            var result = converter.Convert(expiringDate, typeof(Brush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

            Assert.NotNull(result);
            Assert.Equal(ColorConverter.ConvertFromString("#92400E"), result.Color);
        }

        [Fact]
        public void Convert_ValidFutureDate_ReturnsGreenBrush()
        {
            var converter = new ExpiryColorConverter();
            var validDate = DateTime.Today.AddDays(100);
            var result = converter.Convert(validDate, typeof(Brush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

            Assert.NotNull(result);
            Assert.Equal(ColorConverter.ConvertFromString("#065F46"), result.Color);
        }
    }
}
