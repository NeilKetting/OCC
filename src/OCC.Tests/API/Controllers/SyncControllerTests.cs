using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.Shared.Framework;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class SyncControllerTests
    {
        private readonly AppDbContext _context;
        private readonly Mock<ILogger<SyncController>> _loggerMock;
        private readonly SyncController _controller;

        public SyncControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _loggerMock = new Mock<ILogger<SyncController>>();
            _controller = new SyncController(_context, _loggerMock.Object);
        }

        [Fact]
        public async Task PushSync_ValidRequest_ReturnsSuccessApiResponse()
        {
            // Arrange
            var request = new SyncPushRequest
            {
                DeviceId = "TABLET-BAY-01",
                UserId = "user-site-mgr",
                Changes = new List<SyncEntityChange>
                {
                    new SyncEntityChange
                    {
                        EntityName = "AttendanceRecord",
                        EntityId = Guid.NewGuid(),
                        Action = SyncAction.Create,
                        JsonPayload = "{}"
                    }
                }
            };

            // Act
            var actionResult = await _controller.PushSync(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var apiResponse = Assert.IsType<ApiResponse<SyncPushResponse>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Single(apiResponse.Data.Results);
            Assert.True(apiResponse.Data.Results[0].Applied);
        }

        [Fact]
        public async Task PushSync_NullPayload_ReturnsBadRequest()
        {
            // Act
            var actionResult = await _controller.PushSync(null!);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            var apiResponse = Assert.IsType<ApiResponse<SyncPushResponse>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
        }

        [Fact]
        public async Task PullSync_ValidRequest_ReturnsDeltaSyncResponse()
        {
            // Arrange
            var request = new SyncPullRequest
            {
                DeviceId = "TABLET-BAY-01",
                UserId = "user-site-mgr",
                LastSyncTimestampUtc = DateTime.UtcNow.AddHours(-1)
            };

            // Act
            var actionResult = await _controller.PullSync(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var apiResponse = Assert.IsType<ApiResponse<SyncPullResponse>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.False(apiResponse.Data.HasMoreChanges);
        }
    }
}
