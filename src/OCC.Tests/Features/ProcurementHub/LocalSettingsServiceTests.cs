using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using Xunit;

namespace OCC.Tests.Features.ProcurementHub
{
    public class LocalSettingsServiceTests
    {
        private readonly LocalSettingsService _service;

        public LocalSettingsServiceTests()
        {
            var mockToast = new Mock<IToastService>();
            var logger = NullLogger<LocalSettingsService>.Instance;
            _service = new LocalSettingsService(logger, mockToast.Object);
        }

        [Fact]
        public void AddCustomProjectHistory_AddsAndDeduplicatesProjects()
        {
            // Arrange
            _service.Settings.CustomProjectHistory.Clear();

            // Act
            _service.AddCustomProjectHistory("Table Bay Site A");
            _service.AddCustomProjectHistory("Table Bay Site B");
            _service.AddCustomProjectHistory("table bay site a"); // Case insensitive duplicate

            // Assert
            var history = _service.Settings.CustomProjectHistory;
            Assert.NotNull(history);
            Assert.Equal(2, history.Count);
            Assert.Equal("table bay site a", history[0]); // Re-inserted at top
            Assert.Equal("Table Bay Site B", history[1]);

            // Cleanup
            _service.RemoveCustomProjectHistory("table bay site a");
            _service.RemoveCustomProjectHistory("Table Bay Site B");
        }

        [Fact]
        public void RemoveCustomProjectHistory_RemovesMatchingProjectEntry()
        {
            // Arrange
            _service.AddCustomProjectHistory("Mistyped Project Name");
            _service.AddCustomProjectHistory("Valid Project Name");

            // Act
            _service.RemoveCustomProjectHistory("Mistyped Project Name");

            // Assert
            var history = _service.Settings.CustomProjectHistory;
            Assert.DoesNotContain("Mistyped Project Name", history);
            Assert.Contains("Valid Project Name", history);

            // Cleanup
            _service.RemoveCustomProjectHistory("Valid Project Name");
        }
    }
}
