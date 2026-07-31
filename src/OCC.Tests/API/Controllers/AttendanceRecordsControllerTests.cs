using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    /// <summary>
    /// Unit tests for AttendanceRecordsController pagination and progressive loading.
    /// </summary>
    public class AttendanceRecordsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<AttendanceRecordsController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public AttendanceRecordsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<AttendanceRecordsController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
        }

        [Fact]
        public async Task GetAttendanceRecords_WithTakeAndSkip_ReturnsPaginatedResults()
        {
            // Arrange
            using var context = new AppDbContext(_dbOptions);
            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var baseDate = new DateTime(2026, 1, 1);
            for (int i = 0; i < 150; i++)
            {
                context.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = Guid.NewGuid(),
                    Date = baseDate.AddDays(i),
                    Status = AttendanceStatus.Present
                });
            }
            await context.SaveChangesAsync();

            // Act - Fetch first page (top 100)
            var actionResult1 = await controller.GetAttendanceRecords(take: 100);
            var page1 = Assert.IsAssignableFrom<IEnumerable<AttendanceRecord>>(actionResult1.Value);
            var page1List = page1.ToList();

            // Act - Fetch remaining page (skip 100)
            var actionResult2 = await controller.GetAttendanceRecords(skip: 100);
            var page2 = Assert.IsAssignableFrom<IEnumerable<AttendanceRecord>>(actionResult2.Value);
            var page2List = page2.ToList();

            // Assert
            Assert.Equal(100, page1List.Count);
            Assert.Equal(50, page2List.Count);
            Assert.True(page1List.First().Date > page1List.Last().Date, "Should be ordered by Date descending");
            Assert.True(page1List.Last().Date > page2List.First().Date, "Page 1 records should be newer than Page 2 records");
        }

        [Fact]
        public async Task GetAttendanceRecords_WithDateFilterAndTake_ReturnsFilteredPaginatedResults()
        {
            // Arrange
            using var context = new AppDbContext(_dbOptions);
            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var fromDate = new DateTime(2026, 7, 1);
            var toDate = new DateTime(2026, 7, 31);

            for (int i = 0; i < 50; i++)
            {
                context.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = Guid.NewGuid(),
                    Date = fromDate.AddDays(i % 30),
                    Status = AttendanceStatus.Present
                });
            }
            await context.SaveChangesAsync();

            // Act
            var actionResult = await controller.GetAttendanceRecords(from: fromDate, to: toDate, take: 20);
            var records = Assert.IsAssignableFrom<IEnumerable<AttendanceRecord>>(actionResult.Value).ToList();

            // Assert
            Assert.Equal(20, records.Count);
            Assert.All(records, r => Assert.True(r.Date >= fromDate && r.Date <= toDate));
        }
    }
}
