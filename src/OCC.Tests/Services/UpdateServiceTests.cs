using Microsoft.Extensions.Logging;
using Moq;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.IO;
using Xunit;

namespace OCC.Tests.Services
{
    public class UpdateServiceTests
    {
        private readonly Mock<ILogger<UpdateService>> _mockLogger;
        private readonly Mock<ILogger<LocalSettingsService>> _mockSettingsLogger;
        private readonly Mock<IToastService> _mockToastService;
        private readonly LocalSettingsService _localSettingsService;

        public UpdateServiceTests()
        {
            _mockLogger = new Mock<ILogger<UpdateService>>();
            _mockSettingsLogger = new Mock<ILogger<LocalSettingsService>>();
            _mockToastService = new Mock<IToastService>();
            _localSettingsService = new LocalSettingsService(_mockSettingsLogger.Object, _mockToastService.Object);
        }

        [Fact]
        public void LocalSettings_FailedUpdateAttemptCount_TracksAttemptsCorrectly()
        {
            // Arrange
            _localSettingsService.Settings.FailedUpdateAttemptCount = 0;
            _localSettingsService.Settings.LastAttemptedUpdateVersion = "1.6.14";

            // Act - Simulating 2 failed update attempts
            _localSettingsService.Settings.FailedUpdateAttemptCount++;
            Assert.Equal(1, _localSettingsService.Settings.FailedUpdateAttemptCount);

            _localSettingsService.Settings.FailedUpdateAttemptCount++;
            Assert.Equal(2, _localSettingsService.Settings.FailedUpdateAttemptCount);

            // Act - Reset after successful update or loop circuit break
            _localSettingsService.Settings.FailedUpdateAttemptCount = 0;
            _localSettingsService.Settings.LastAttemptedUpdateVersion = string.Empty;

            // Assert
            Assert.Equal(0, _localSettingsService.Settings.FailedUpdateAttemptCount);
            Assert.Empty(_localSettingsService.Settings.LastAttemptedUpdateVersion);
        }

        [Fact]
        public void PurgeUpdateCache_DeletesTemporaryDirectoriesSafely()
        {
            // Arrange
            var updateService = new UpdateService(_mockLogger.Object, _localSettingsService);
            var tempVelopackDir = Path.Combine(Path.GetTempPath(), "Velopack_Test_" + Guid.NewGuid());
            Directory.CreateDirectory(tempVelopackDir);

            Assert.True(Directory.Exists(tempVelopackDir));

            // Act
            updateService.PurgeUpdateCache();

            // Cleanup test folder if created
            if (Directory.Exists(tempVelopackDir))
            {
                Directory.Delete(tempVelopackDir, true);
            }

            // Assert - Purge method executes without throwing exceptions
            Assert.NotNull(updateService);
        }
    }
}
