using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Middleware;
using OCC.Shared.Framework;
using Xunit;

namespace OCC.Tests.Framework
{
    public class FrameworkTests
    {
        [Fact]
        public void ApiResponse_Ok_ReturnsSuccessPayload()
        {
            // Arrange & Act
            var response = ApiResponse<string>.Ok("Test Data", "Operation Successful");

            // Assert
            Assert.True(response.Success);
            Assert.Equal("Test Data", response.Data);
            Assert.Equal("Operation Successful", response.Message);
            Assert.Empty(response.Errors);
            Assert.False(string.IsNullOrEmpty(response.TraceId));
        }

        [Fact]
        public void ApiResponse_Fail_ReturnsFailurePayload()
        {
            // Arrange
            var errors = new[] { "Error line 1", "Error line 2" };

            // Act
            var response = ApiResponse<string>.Fail("Operation Failed", errors);

            // Assert
            Assert.False(response.Success);
            Assert.Null(response.Data);
            Assert.Equal("Operation Failed", response.Message);
            Assert.Equal(2, response.Errors.Count);
            Assert.Equal("Error line 1", response.Errors[0]);
        }

        [Fact]
        public void PagedResult_CalculatesTotalPagesCorrectly()
        {
            // Arrange
            var items = new List<string> { "item1", "item2", "item3" };

            // Act
            var pagedResult = PagedResult<string>.Create(items, totalCount: 25, pageIndex: 1, pageSize: 10);

            // Assert
            Assert.Equal(3, pagedResult.TotalPages); // 25 total / 10 page size = 3 pages
            Assert.True(pagedResult.HasNextPage);
            Assert.False(pagedResult.HasPreviousPage);
            Assert.Equal(3, pagedResult.Items.Count);
        }

        [Fact]
        public void SyncPushRequest_Serialization_RoundTripsAccurately()
        {
            // Arrange
            var request = new SyncPushRequest
            {
                DeviceId = "TABLET-001",
                UserId = "user-123",
                Changes = new List<SyncEntityChange>
                {
                    new SyncEntityChange
                    {
                        EntityName = "AttendanceRecord",
                        EntityId = Guid.NewGuid(),
                        Action = SyncAction.Create,
                        JsonPayload = "{\"ClockIn\":\"2026-07-28\"}"
                    }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(request);
            var deserialized = JsonSerializer.Deserialize<SyncPushRequest>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("TABLET-001", deserialized.DeviceId);
            Assert.Single(deserialized.Changes);
            Assert.Equal(SyncAction.Create, deserialized.Changes[0].Action);
        }

        [Fact]
        public async Task GlobalExceptionMiddleware_CatchesUnhandledException_Returns500ApiResponse()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<GlobalExceptionMiddleware>>();
            var envMock = new Mock<IHostEnvironment>();
            envMock.Setup(e => e.EnvironmentName).Returns("Development");

            RequestDelegate next = (HttpContext ctx) => throw new InvalidOperationException("Test exception");

            var middleware = new GlobalExceptionMiddleware(next, loggerMock.Object, envMock.Object);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            var responseText = await reader.ReadToEndAsync();

            var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(apiResponse);
            Assert.False(apiResponse.Success);
            Assert.Equal("Test exception", apiResponse.Message);
        }
    }
}
