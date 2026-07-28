using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using OCC.Mobile.Services;
using OCC.Shared.Framework;
using Xunit;

namespace OCC.Tests.Services
{
    public class OfflineSyncEngineTests : IDisposable
    {
        private readonly string _tempDirectory;

        public OfflineSyncEngineTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "OCC_Test_Sync_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public async Task QueueChangeAsync_AddsItemToQueueAndPersists()
        {
            // Arrange
            var engine = new OfflineSyncEngine(new HttpClient(), _tempDirectory);
            var change = new SyncEntityChange
            {
                EntityName = "AttendanceRecord",
                EntityId = Guid.NewGuid(),
                Action = SyncAction.Create,
                JsonPayload = "{\"EmployeeId\":\"emp-001\"}"
            };

            // Act
            await engine.QueueChangeAsync(change);
            var count = await engine.GetPendingCountAsync();
            var pending = await engine.GetPendingChangesAsync();

            // Assert
            Assert.Equal(1, count);
            Assert.Single(pending);
            Assert.Equal("AttendanceRecord", pending[0].EntityName);
        }

        [Fact]
        public async Task ClearPendingQueueAsync_ClearsAllQueuedItems()
        {
            // Arrange
            var engine = new OfflineSyncEngine(new HttpClient(), _tempDirectory);
            await engine.QueueChangeAsync(new SyncEntityChange { EntityName = "Test" });

            // Act
            await engine.ClearPendingQueueAsync();
            var count = await engine.GetPendingCountAsync();

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task SyncPendingChangesAsync_SuccessfulServerResponse_ClearsQueue()
        {
            // Arrange
            var handlerMock = new Mock<HttpMessageHandler>();
            var expectedPushResponse = ApiResponse<SyncPushResponse>.Ok(new SyncPushResponse
            {
                Success = true,
                Message = "Synced successfully"
            });

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedPushResponse)
            };

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var httpClient = new HttpClient(handlerMock.Object);
            var engine = new OfflineSyncEngine(httpClient, _tempDirectory);

            await engine.QueueChangeAsync(new SyncEntityChange { EntityName = "AttendanceRecord" });

            // Act
            var syncResult = await engine.SyncPendingChangesAsync("TABLET-01", "USER-01", "https://api.occ.co.za");

            // Assert
            Assert.NotNull(syncResult);
            Assert.True(syncResult.Success);
            Assert.Equal(0, await engine.GetPendingCountAsync());
        }
    }
}
