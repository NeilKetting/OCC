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
    public class ClockingV2ControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<ClockingV2Controller>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public ClockingV2ControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<ClockingV2Controller>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        }

        [Fact]
        public async Task ClockIn_ValidRequest_DualWritesTimesheetAndLegacyAttendance()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee { Id = empId, FirstName = "John", LastName = "Clock", HourlyRate = 100 });
            await context.SaveChangesAsync();

            var controller = new ClockingV2Controller(context, _mockHubContext.Object, _mockLogger.Object);

            var request = new ClockingEventRequest
            {
                EmployeeId = empId,
                Timestamp = DateTime.Now,
                Source = "MobileApp"
            };

            var result = await controller.ClockIn(request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var clockEvent = Assert.IsType<ClockingEvent>(okResult.Value);
            Assert.Equal(empId, clockEvent.EmployeeId);
            Assert.Equal(ClockEventType.ClockIn, clockEvent.EventType);

            var timesheet = await context.DailyTimesheets.FirstOrDefaultAsync(t => t.EmployeeId == empId);
            Assert.NotNull(timesheet);
            Assert.Equal(TimesheetStatus.Present, timesheet!.Status);

            var legacy = await context.AttendanceRecords.FirstOrDefaultAsync(r => r.EmployeeId == empId);
            Assert.NotNull(legacy);
            Assert.Equal(AttendanceStatus.Present, legacy!.Status);
        }

        [Fact]
        public async Task ClockIn_EmptyEmployeeId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ClockingV2Controller(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.ClockIn(new ClockingEventRequest { EmployeeId = Guid.Empty });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ClockOut_ValidRequest_CalculatesHoursAndWage()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            var emp = new Employee { Id = empId, FirstName = "Worker", LastName = "Out", HourlyRate = 50.0m };
            context.Employees.Add(emp);

            var today = DateTime.Today;
            var inTime = today.AddHours(7);
            var outTime = today.AddHours(16); // 9 hours total, 5+ hours so 0.75 lunch deducted = 8.25 net hours

            context.DailyTimesheets.Add(new DailyTimesheet
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                Date = today,
                FirstInTime = inTime,
                Status = TimesheetStatus.Present
            });

            context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                Date = today,
                CheckInTime = inTime,
                Status = AttendanceStatus.Present
            });

            await context.SaveChangesAsync();

            var controller = new ClockingV2Controller(context, _mockHubContext.Object, _mockLogger.Object);

            var request = new ClockingEventRequest
            {
                EmployeeId = empId,
                Timestamp = outTime,
                Source = "Portal"
            };

            var result = await controller.ClockOut(request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var clockEvent = Assert.IsType<ClockingEvent>(okResult.Value);
            Assert.Equal(ClockEventType.ClockOut, clockEvent.EventType);

            var timesheet = await context.DailyTimesheets.FirstOrDefaultAsync(t => t.EmployeeId == empId);
            Assert.Equal(8.25m, timesheet!.CalculatedHours);
            Assert.Equal(412.50m, timesheet.WageEstimated);

            var legacy = await context.AttendanceRecords.FirstOrDefaultAsync(r => r.EmployeeId == empId);
            Assert.Equal(outTime, legacy!.CheckOutTime);
        }

        [Fact]
        public async Task RepairSyncV2_FixesStaleSessions_ReturnsRepairedCount()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            var today = DateTime.Today;

            context.ClockingEvents.Add(new ClockingEvent
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                Timestamp = today.AddHours(7),
                EventType = ClockEventType.ClockIn
            });

            context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                Date = today,
                CheckInTime = today.AddHours(7),
                CheckOutTime = today.AddHours(16)
            });

            await context.SaveChangesAsync();

            var controller = new ClockingV2Controller(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.RepairSyncV2();

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetActivePhysicalPresence_ReturnsActiveClockedInEvents()
        {
            using var context = new AppDbContext(_dbOptions);
            var emp1 = Guid.NewGuid();
            var emp2 = Guid.NewGuid();

            context.ClockingEvents.AddRange(
                new ClockingEvent { Id = Guid.NewGuid(), EmployeeId = emp1, Timestamp = DateTime.Now, EventType = ClockEventType.ClockIn },
                new ClockingEvent { Id = Guid.NewGuid(), EmployeeId = emp2, Timestamp = DateTime.Now.AddHours(-2), EventType = ClockEventType.ClockIn },
                new ClockingEvent { Id = Guid.NewGuid(), EmployeeId = emp2, Timestamp = DateTime.Now.AddHours(-1), EventType = ClockEventType.ClockOut }
            );
            await context.SaveChangesAsync();

            var controller = new ClockingV2Controller(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetActivePhysicalPresence();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var activeEvents = Assert.IsAssignableFrom<IEnumerable<ClockingEvent>>(okResult.Value).ToList();
            Assert.Single(activeEvents);
            Assert.Equal(emp1, activeEvents[0].EmployeeId);
        }

        [Fact]
        public async Task GetTimesheetsByRange_InvalidRange_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new ClockingV2Controller(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetTimesheetsByRange(DateTime.Today.AddDays(5), DateTime.Today);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
