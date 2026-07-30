using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using OCC.API.Controllers.HR;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Features.WagesHub
{
    public class WageSettingsControllerTests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private IHubContext<NotificationHub> GetMockHubContext()
        {
            var mockHub = new Mock<IHubContext<NotificationHub>>();
            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();

            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);

            return mockHub.Object;
        }

        [Fact]
        public async Task GetSettings_WhenNoSettingsExist_CreatesAndReturnsDefaultSettings()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new WageSettingsController(context, GetMockHubContext());

            // Act
            var result = await controller.GetSettings();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var settings = Assert.IsType<WageSettings>(okResult.Value);
            Assert.Equal(PayFrequency.Weekly, settings.CptDefaultPayFrequency);
            Assert.Equal(PayFrequency.Fortnightly, settings.JhbDefaultPayFrequency);
            Assert.Equal(28.75m, settings.BibcRatePerDay);
        }

        [Fact]
        public async Task UpdateSettings_UpdatesExistingWageSettings()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new WageSettingsController(context, GetMockHubContext());
            await controller.GetSettings(); // initialize default

            var updated = new WageSettings
            {
                CptDefaultPayFrequency = PayFrequency.Weekly,
                JhbDefaultPayFrequency = PayFrequency.Weekly,
                BibcRatePerDay = 32.50m,
                DefaultSupervisorFee = 600m,
                AutoRecoverAdHocAdvances = true
            };

            // Act
            var result = await controller.UpdateSettings(updated);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var settings = Assert.IsType<WageSettings>(okResult.Value);
            Assert.Equal(32.50m, settings.BibcRatePerDay);
            Assert.Equal(600m, settings.DefaultSupervisorFee);
            Assert.Equal(PayFrequency.Weekly, settings.JhbDefaultPayFrequency);
        }
    }
}
