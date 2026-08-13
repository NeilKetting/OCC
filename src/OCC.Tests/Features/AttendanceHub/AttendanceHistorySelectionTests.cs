using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Features.AttendanceHub.ViewModels;
using OCC.WpfClient.Services.Interfaces;
using Xunit;

namespace OCC.Tests.Features.AttendanceHub
{
    public class AttendanceHistorySelectionTests
    {
        [Fact]
        public async Task SelectionIsPreservedAcrossSearchFiltersAndRecalculatesTotals()
        {
            // Arrange
            var mockAttendance = new Mock<IAttendanceService>();
            var mockEmployee = new Mock<IEmployeeService>();
            var mockProject = new Mock<IProjectService>();
            var mockDialog = new Mock<IDialogService>();
            var mockPdf = new Mock<IPdfService>();
            var mockSignalR = new Mock<ISignalRService>();

            var emp1Id = Guid.NewGuid();
            var emp2Id = Guid.NewGuid();

            var employees = new List<EmployeeSummaryDto>
            {
                new EmployeeSummaryDto { Id = emp1Id, FirstName = "Aaron", LastName = "Moselane", EmploymentType = EmploymentType.Permanent, RateType = RateType.Hourly },
                new EmployeeSummaryDto { Id = emp2Id, FirstName = "Andrew", LastName = "Masilela", EmploymentType = EmploymentType.Permanent, RateType = RateType.Hourly }
            };

            var records = new List<AttendanceRecord>
            {
                new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = emp1Id, Date = DateTime.Today, Status = AttendanceStatus.Present, CheckInTime = DateTime.Today.AddHours(7), CheckOutTime = DateTime.Today.AddHours(16) },
                new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = emp2Id, Date = DateTime.Today, Status = AttendanceStatus.Present, CheckInTime = DateTime.Today.AddHours(7), CheckOutTime = DateTime.Today.AddHours(16) }
            };

            mockEmployee.Setup(s => s.GetEmployeesAsync()).ReturnsAsync(employees);
            mockProject.Setup(s => s.GetProjectSummariesAsync(It.IsAny<bool>())).ReturnsAsync(new List<ProjectSummaryDto>());
            mockAttendance.Setup(s => s.GetAttendanceRecordsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int?>())).ReturnsAsync(records);

            var vm = new AttendanceHistoryListViewModel(
                mockAttendance.Object,
                mockEmployee.Object,
                mockProject.Object,
                mockDialog.Object,
                mockPdf.Object,
                NullLogger<AttendanceHistoryListViewModel>.Instance,
                mockSignalR.Object);

            await vm.LoadDataAsync();

            // Act 1: Search "Aaron" and select Aaron's row
            vm.SearchQuery = "Aaron";
            Assert.Single(vm.Items);
            var aaronRow = vm.Items.First();
            Assert.Equal("Aaron Moselane", aaronRow.EmployeeName);

            aaronRow.IsSelected = true;

            // Totals should now reflect the 1 selected record
            Assert.Equal(1, vm.TotalCount);

            // Act 2: Search "Andrew" without clearing selection
            vm.SearchQuery = "Andrew";
            Assert.Single(vm.Items);
            var andrewRow = vm.Items.First();
            Assert.Equal("Andrew Masilela", andrewRow.EmployeeName);

            // Select Andrew's row as well
            andrewRow.IsSelected = true;

            // Totals should now reflect the 2 selected records across search queries
            Assert.Equal(2, vm.TotalCount);

            // Act 3: Clear search query
            vm.SearchQuery = string.Empty;
            Assert.Equal(2, vm.Items.Count);

            // Verify both rows are checked
            Assert.True(vm.Items.All(r => r.IsSelected));
            Assert.Equal(2, vm.TotalCount);
        }
    }
}
