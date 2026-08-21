using Microsoft.Extensions.Logging;
using Moq;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Features.CalendarHub.Models;
using OCC.WpfClient.Features.CalendarHub.Services;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Features.CalendarHub
{
    public class CalendarServiceTests
    {
        private readonly Mock<IProjectTaskService> _mockTaskService = new();
        private readonly Mock<IProjectService> _mockProjectService = new();
        private readonly Mock<IEmployeeService> _mockEmployeeService = new();
        private readonly Mock<IHolidayService> _mockHolidayService = new();
        private readonly Mock<ILeaveService> _mockLeaveService = new();
        private readonly Mock<IOrderService> _mockOrderService = new();
        private readonly Mock<ILogger<CalendarService>> _mockLogger = new();

        public CalendarServiceTests()
        {
            _mockTaskService.Setup(s => s.GetTasksAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ProjectTask>());

            _mockProjectService.Setup(s => s.GetProjectSummariesAsync())
                .ReturnsAsync(new List<ProjectSummaryDto>());

            _mockEmployeeService.Setup(s => s.GetEmployeesAsync())
                .ReturnsAsync(new List<EmployeeSummaryDto>());

            _mockHolidayService.Setup(s => s.GetHolidaysForYearAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<PublicHoliday>());

            _mockLeaveService.Setup(s => s.GetLeaveRequestsAsync())
                .ReturnsAsync(new List<LeaveRequest>());
        }

        [Fact]
        public async Task GetEventsAsync_IncludesProcurementDeliveriesMatchingProjectFilter()
        {
            // Arrange
            var projectId1 = Guid.NewGuid();
            var projectId2 = Guid.NewGuid();
            var deliveryDate = new DateTime(2026, 8, 15);

            var orders = new List<Order>
            {
                new Order
                {
                    Id = Guid.NewGuid(),
                    OrderNumber = "PO-001",
                    SupplierName = "Supplier A",
                    ExpectedDeliveryDate = deliveryDate,
                    ProjectId = projectId1,
                    ProjectName = "Project Alpha",
                    Status = OrderStatus.Ordered
                },
                new Order
                {
                    Id = Guid.NewGuid(),
                    OrderNumber = "PO-002",
                    SupplierName = "Supplier B",
                    ExpectedDeliveryDate = deliveryDate,
                    ProjectId = projectId2,
                    ProjectName = "Project Beta",
                    Status = OrderStatus.Ordered
                },
                new Order
                {
                    Id = Guid.NewGuid(),
                    OrderNumber = "PO-003",
                    SupplierName = "Supplier C",
                    ExpectedDeliveryDate = deliveryDate,
                    ProjectId = null,
                    ProjectName = null,
                    Status = OrderStatus.Ordered
                }
            };

            _mockOrderService.Setup(s => s.GetOrdersAsync()).ReturnsAsync(orders);

            var calendarService = new CalendarService(
                _mockTaskService.Object,
                _mockProjectService.Object,
                _mockEmployeeService.Object,
                _mockHolidayService.Object,
                _mockLeaveService.Object,
                _mockOrderService.Object,
                _mockLogger.Object);

            var windowStart = new DateTime(2026, 8, 1);
            var windowEnd = new DateTime(2026, 8, 31);

            // Act - Filter for projectId1 only
            var events = await calendarService.GetEventsAsync(windowStart, windowEnd, new[] { projectId1 });

            // Assert
            var procurementEvents = events.Where(e => e.Type == CalendarEventType.OrderDelivery).ToList();
            Assert.Equal(2, procurementEvents.Count); // Project 1 order + Stock order
            Assert.Contains(procurementEvents, e => e.Title.Contains("PO #PO-001"));
            Assert.Contains(procurementEvents, e => e.Title.Contains("PO #PO-003"));
            Assert.DoesNotContain(procurementEvents, e => e.Title.Contains("PO #PO-002"));
        }
    }
}
