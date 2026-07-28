using Microsoft.AspNetCore.Http;
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
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
                .Options;

            _mockLogger = new Mock<ILogger<AttendanceRecordsController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        }

        [Fact]
        public async Task GetAttendanceRecords_DateFilter_ReturnsFilteredList()
        {
            using var context = new AppDbContext(_dbOptions);
            var today = DateTime.Today;
            context.AttendanceRecords.AddRange(
                new AttendanceRecord { Id = Guid.NewGuid(), Date = today.AddDays(-5), Status = AttendanceStatus.Present },
                new AttendanceRecord { Id = Guid.NewGuid(), Date = today.AddDays(-2), Status = AttendanceStatus.Present },
                new AttendanceRecord { Id = Guid.NewGuid(), Date = today, Status = AttendanceStatus.Present }
            );
            await context.SaveChangesAsync();

            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetAttendanceRecords(today.AddDays(-3), today);

            var okResult = Assert.IsAssignableFrom<IEnumerable<AttendanceRecord>>(result.Value);
            Assert.Equal(2, okResult.Count());
        }

        [Fact]
        public async Task GetAttendanceRecords_InvalidDateRange_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetAttendanceRecords(DateTime.Today.AddDays(2), DateTime.Today);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetAttendanceRecord_ValidId_ReturnsRecord()
        {
            using var context = new AppDbContext(_dbOptions);
            var recId = Guid.NewGuid();
            context.AttendanceRecords.Add(new AttendanceRecord { Id = recId, Date = DateTime.Today, Status = AttendanceStatus.Present });
            await context.SaveChangesAsync();

            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetAttendanceRecord(recId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var record = Assert.IsType<AttendanceRecord>(okResult.Value);
            Assert.Equal(recId, record.Id);
        }

        [Fact]
        public async Task PostAttendanceRecord_ValidRecord_CalculatesHoursAndCreatesLeaveIfAbsent()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            var date = DateTime.Today.AddDays(-1);

            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var record = new AttendanceRecord
            {
                EmployeeId = empId,
                Date = date,
                Status = AttendanceStatus.Absent
            };

            var result = await controller.PostAttendanceRecord(record);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdRec = Assert.IsType<AttendanceRecord>(createdResult.Value);
            Assert.Equal(0, createdRec.HoursWorked);

            // Verify AWOL leave request was created automatically
            var autoLeave = await context.LeaveRequests.FirstOrDefaultAsync(l => l.EmployeeId == empId && l.StartDate == date);
            Assert.NotNull(autoLeave);
            Assert.Equal(LeaveType.AbsentWithoutLeave, autoLeave!.LeaveType);
        }

        [Fact]
        public async Task PostAttendanceRecord_FutureClockIn_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var record = new AttendanceRecord
            {
                EmployeeId = Guid.NewGuid(),
                Date = DateTime.Today,
                CheckInTime = DateTime.Now.AddHours(2),
                Status = AttendanceStatus.Present
            };

            var result = await controller.PostAttendanceRecord(record);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("future", badReq.Value!.ToString());
        }

        [Fact]
        public async Task PutAttendanceRecord_ValidUpdate_RecalculatesHoursWorked()
        {
            using var context = new AppDbContext(_dbOptions);
            var recId = Guid.NewGuid();
            var empId = Guid.NewGuid();
            var date = new DateTime(2026, 6, 22); // Monday

            var existing = new AttendanceRecord
            {
                Id = recId,
                EmployeeId = empId,
                Date = date,
                CheckInTime = date.AddHours(7),
                CheckOutTime = date.AddHours(12), // 5 hours, no lunch
                Status = AttendanceStatus.Present
            };
            context.AttendanceRecords.Add(existing);
            await context.SaveChangesAsync();

            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var update = new AttendanceRecord
            {
                Id = recId,
                EmployeeId = empId,
                Date = date,
                CheckInTime = date.AddHours(7),
                CheckOutTime = date.AddHours(16), // 9 hours total - 1 hour lunch = 8 hours
                Status = AttendanceStatus.Present
            };

            var result = await controller.PutAttendanceRecord(recId, update);

            Assert.IsType<NoContentResult>(result);

            var recInDb = await context.AttendanceRecords.FindAsync(recId);
            Assert.Equal(8.0, recInDb!.HoursWorked);
        }

        [Fact]
        public async Task DeleteAttendanceRecord_AbsentRecord_RemovesAutoLeaveRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var recId = Guid.NewGuid();
            var empId = Guid.NewGuid();
            var date = DateTime.Today.AddDays(-1);

            context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = recId,
                EmployeeId = empId,
                Date = date,
                Status = AttendanceStatus.Absent
            });

            var autoLeave = new LeaveRequest
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                StartDate = date,
                EndDate = date,
                LeaveType = LeaveType.AbsentWithoutLeave,
                Reason = "UNPAID -Absent without leave"
            };
            context.LeaveRequests.Add(autoLeave);
            await context.SaveChangesAsync();

            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.DeleteAttendanceRecord(recId);

            Assert.IsType<NoContentResult>(result);

            var leaveInDb = await context.LeaveRequests.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == autoLeave.Id);
            Assert.True(leaveInDb == null || !leaveInDb.IsActive);
        }

        [Fact]
        public async Task UploadNote_ValidFile_ReturnsRelativePath()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var content = "Doctor note content text";
            var fileName = "doctor_note.pdf";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var formFile = new FormFile(stream, 0, stream.Length, "file", fileName);

            var result = await controller.UploadNote(formFile);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var path = okResult.Value!.ToString();
            Assert.StartsWith("/uploads/notes/", path);
            Assert.EndsWith(".pdf", path);
        }

        [Fact]
        public async Task UploadNote_InvalidExtension_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new AttendanceRecordsController(context, _mockHubContext.Object, _mockLogger.Object);

            var content = "Malicious executable content";
            var fileName = "script.exe";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var formFile = new FormFile(stream, 0, stream.Length, "file", fileName);

            var result = await controller.UploadNote(formFile);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
