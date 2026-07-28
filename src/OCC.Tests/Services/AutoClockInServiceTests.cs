using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Data;
using OCC.API.Services;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Services
{
    public class AutoClockInServiceTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<AutoClockInService>> _mockLogger;

        public AutoClockInServiceTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<AutoClockInService>>();
        }

        private IServiceProvider CreateServiceProvider(AppDbContext context)
        {
            var services = new ServiceCollection();
            services.AddSingleton(context);
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task ProcessClockInAsync_FeatureDisabled_NoActionTaken()
        {
            using var context = new AppDbContext(_dbOptions);
            context.AppSettings.Add(new AppSetting
            {
                Key = "CompanyProfile",
                Value = JsonSerializer.Serialize(new CompanyDetails { AutoClockInEnabled = false })
            });
            await context.SaveChangesAsync();

            var serviceProvider = CreateServiceProvider(context);
            var service = new AutoClockInService(serviceProvider, _mockLogger.Object);

            await service.ProcessClockInAsync();

            var count = await context.AttendanceRecords.CountAsync();
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task ProcessClockInAsync_FeatureEnabled_ProcessesClockInForActiveEmployees()
        {
            using var context = new AppDbContext(_dbOptions);
            var empId = Guid.NewGuid();
            context.Employees.Add(new Employee
            {
                Id = empId,
                FirstName = "Auto",
                LastName = "User",
                Status = EmployeeStatus.Active,
                ShiftStartTime = new TimeSpan(0, 0, 0), // midnight so currentTime >= shiftStartTime
                ShiftEndTime = new TimeSpan(23, 59, 59)
            });

            // South Africa time today
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            var saToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone).Date;

            var companyDetails = new CompanyDetails
            {
                AutoClockInEnabled = true,
                AutoClockInDays = new List<DayOfWeek> { saToday.DayOfWeek }
            };

            context.AppSettings.Add(new AppSetting
            {
                Key = "CompanyProfile",
                Value = JsonSerializer.Serialize(companyDetails)
            });

            await context.SaveChangesAsync();

            var serviceProvider = CreateServiceProvider(context);
            var service = new AutoClockInService(serviceProvider, _mockLogger.Object);

            await service.ProcessClockInAsync();

            var attRecord = await context.AttendanceRecords.FirstOrDefaultAsync(a => a.EmployeeId == empId);
            Assert.NotNull(attRecord);
            Assert.True(attRecord!.IsAutoClockIn);
            Assert.Equal(AttendanceStatus.Present, attRecord.Status);

            var v2Timesheet = await context.DailyTimesheets.FirstOrDefaultAsync(t => t.EmployeeId == empId);
            Assert.NotNull(v2Timesheet);

            var clockEvent = await context.ClockingEvents.FirstOrDefaultAsync(c => c.EmployeeId == empId);
            Assert.NotNull(clockEvent);
            Assert.Equal(ClockEventType.ClockIn, clockEvent!.EventType);
        }
    }
}
