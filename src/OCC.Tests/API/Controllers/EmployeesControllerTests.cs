using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.DTOs;
using OCC.Shared.Framework;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class EmployeesControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<EmployeesController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public EmployeesControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<EmployeesController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        }

        [Fact]
        public async Task GetEmployees_ReturnsOrderedSummaries()
        {
            using var context = new AppDbContext(_dbOptions);
            context.Employees.AddRange(
                new Employee { Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Zeta", Role = EmployeeRole.GeneralWorker },
                new Employee { Id = Guid.NewGuid(), FirstName = "Bob", LastName = "Alpha", Role = EmployeeRole.Supervisor }
            );
            await context.SaveChangesAsync();

            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.GetEmployees();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var summaries = Assert.IsAssignableFrom<IEnumerable<EmployeeSummaryDto>>(okResult.Value).ToList();
            Assert.Equal(2, summaries.Count);
            Assert.Equal("Alpha", summaries[0].LastName);
            Assert.Equal("Zeta", summaries[1].LastName);
        }

        [Fact]
        public async Task GetEmployee_ValidId_ReturnsDetailDto()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "John",
                LastName = "Smith",
                Email = "john@example.com",
                HourlyRate = 125.50m
            });
            await context.SaveChangesAsync();

            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.GetEmployee(empId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<EmployeeDto>(okResult.Value);
            Assert.Equal(empId, dto.Id);
            Assert.Equal("John", dto.FirstName);
            Assert.Equal(125.50m, dto.HourlyRate);
        }

        [Fact]
        public async Task GetEmployee_InvalidId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.GetEmployee(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetEmployee_NotFound_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.GetEmployee(Guid.NewGuid());

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostEmployee_ValidPayload_CreatesEmployee()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var newEmp = new Employee
            {
                FirstName = " Jane ",
                LastName = " Doe ",
                HourlyRate = 95.0m,
                Branch = "JHB"
            };

            var result = await controller.PostEmployee(newEmp);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<EmployeeDto>(createdResult.Value);
            Assert.Equal("Jane", dto.FirstName);
            Assert.Equal("Doe", dto.LastName);
            Assert.NotEqual(Guid.Empty, dto.Id);
        }

        [Fact]
        public async Task PostEmployee_NullPayload_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.PostEmployee(null!);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostEmployee_MissingNames_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var emp = new Employee { FirstName = "", LastName = "  " };

            var result = await controller.PostEmployee(emp);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PutEmployee_ValidUpdate_UpdatesRecord()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "Mark",
                LastName = "Taylor",
                HourlyRate = 80.0m
            });
            await context.SaveChangesAsync();

            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var updateEmp = new Employee
            {
                Id = empId,
                FirstName = "Mark",
                LastName = "Taylor-Updated",
                HourlyRate = 85.0m
            };

            var result = await controller.PutEmployee(empId, updateEmp);

            Assert.IsType<NoContentResult>(result);

            var updatedInDb = await context.Employees.FindAsync(empId);
            Assert.Equal("Taylor-Updated", updatedInDb!.LastName);
            Assert.Equal(85.0m, updatedInDb.HourlyRate);
        }

        [Fact]
        public async Task PutEmployee_IdMismatch_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var emp = new Employee { Id = Guid.NewGuid(), FirstName = "Test", LastName = "User" };

            var result = await controller.PutEmployee(Guid.NewGuid(), emp);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteEmployee_ActiveTaskAssignment_ReturnsConflict()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee { Id = empId, FirstName = "Worker", LastName = "One", Status = EmployeeStatus.Active });
            context.TaskAssignments.Add(new TaskAssignment { Id = Guid.NewGuid(), AssigneeId = empId, TaskId = Guid.NewGuid() });
            await context.SaveChangesAsync();

            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.DeleteEmployee(empId);

            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("active tasks", conflictResult.Value!.ToString());
        }

        [Fact]
        public async Task DeleteEmployee_NoConflicts_SetsStatusInactive()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee { Id = empId, FirstName = "Clean", LastName = "Employee", Status = EmployeeStatus.Active });
            await context.SaveChangesAsync();

            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.DeleteEmployee(empId);

            Assert.IsType<NoContentResult>(result);

            var emp = await context.Employees.FindAsync(empId);
            Assert.Equal(EmployeeStatus.Inactive, emp!.Status);
        }

        [Fact]
        public async Task GetEmployeeReferences_ReturnsCorrectCounts()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            context.AttendanceRecords.Add(new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = empId, Date = DateTime.Today });
            context.LeaveRequests.Add(new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = empId, StartDate = DateTime.Today, EndDate = DateTime.Today });
            await context.SaveChangesAsync();

            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.GetEmployeeReferences(empId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<EmployeeReferencesDto>(okResult.Value);
            Assert.Equal(1, dto.AttendanceCount);
            Assert.Equal(1, dto.LeaveRequestCount);
            Assert.Equal(0, dto.WageRunCount);
        }

        [Fact]
        public async Task PermanentDeleteEmployee_DeletesRecordAndReferences()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee { Id = empId, FirstName = "ToPurge", LastName = "User" });
            context.AttendanceRecords.Add(new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = empId, Date = DateTime.Today });
            await context.SaveChangesAsync();

            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var result = await controller.PermanentDeleteEmployee(empId);

            Assert.IsType<NoContentResult>(result);

            var emp = await context.Employees.FindAsync(empId);
            Assert.Null(emp);
        }

        [Fact]
        public async Task GetEmployeesPaged_ReturnsPagedApiResponse()
        {
            using var context = new AppDbContext(_dbOptions);
            context.Employees.AddRange(
                new Employee { Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Zeta" },
                new Employee { Id = Guid.NewGuid(), FirstName = "Bob", LastName = "Alpha" }
            );
            await context.SaveChangesAsync();

            var controller = new EmployeesController(context, _mockLogger.Object, _mockHubContext.Object);

            var actionResult = await controller.GetEmployeesPaged(page: 1, pageSize: 10);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var apiResponse = Assert.IsType<ApiResponse<PagedResult<EmployeeSummaryDto>>>(okResult.Value);

            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(2, apiResponse.Data.TotalCount);
            Assert.Equal("Alpha", apiResponse.Data.Items[0].LastName);
        }
    }
}
