using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.Shared.Models;
using OCC.WpfClient.Features.AttendanceHub.ViewModels;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using Xunit;

namespace OCC.Tests.Features.WagesHub
{
    public class WageRunViewModelMonthlyTests
    {
        private readonly Mock<IWageService> _mockWageService;
        private readonly Mock<IPdfService> _mockPdfService;
        private readonly Mock<IExportService> _mockExportService;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly Mock<ILogger<WageRunViewModel>> _mockLogger;
        private readonly LocalSettingsService _localSettings;
        private readonly Mock<IPermissionService> _mockPermissionService;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<IAuthService> _mockAuthService;

        public WageRunViewModelMonthlyTests()
        {
            _mockWageService = new Mock<IWageService>();
            _mockPdfService = new Mock<IPdfService>();
            _mockExportService = new Mock<IExportService>();
            _mockDialogService = new Mock<IDialogService>();
            _mockLogger = new Mock<ILogger<WageRunViewModel>>();

            var mockLocalSettingsLogger = new Mock<ILogger<LocalSettingsService>>();
            var mockToastService = new Mock<IToastService>();
            _localSettings = new LocalSettingsService(mockLocalSettingsLogger.Object, mockToastService.Object);

            _mockPermissionService = new Mock<IPermissionService>();
            _mockSettingsService = new Mock<ISettingsService>();
            _mockAuthService = new Mock<IAuthService>();

            _mockPermissionService.Setup(p => p.CanAccess(It.IsAny<string>())).Returns(true);
            _mockAuthService.Setup(a => a.CurrentUser).Returns(new User { UserRole = UserRole.Admin });
        }

        private WageRunViewModel CreateViewModel()
        {
            return new WageRunViewModel(
                _mockWageService.Object,
                _mockPdfService.Object,
                _mockExportService.Object,
                _mockDialogService.Object,
                _mockLogger.Object,
                _localSettings,
                _mockPermissionService.Object,
                _mockSettingsService.Object,
                _mockAuthService.Object,
                new Mock<ISignalRService>().Object
            );
        }

        [Fact]
        public void SelectingMonthlySalary_SetsStartDateToFirstAndEndDateToLastDayOfMonth()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.SelectedPayType = "MonthlySalary";

            // Assert
            var today = DateTime.Today;
            var expectedStart = new DateTime(today.Year, today.Month, 1);
            var expectedEnd = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

            Assert.Equal(expectedStart, vm.StartDate);
            Assert.Equal(expectedEnd, vm.EndDate);
            Assert.True(vm.IsExcludeZeroWagesToggleVisible);
        }

        [Fact]
        public void ChangingStartDateInMonthlyMode_RecalculatesFullMonthBounds()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedPayType = "MonthlySalary";

            // Act: Change StartDate to mid-month date in October 2026
            vm.StartDate = new DateTime(2026, 10, 15);

            // Assert: StartDate is normalized to 1st, EndDate to 31st
            Assert.Equal(new DateTime(2026, 10, 1), vm.StartDate);
            Assert.Equal(new DateTime(2026, 10, 31), vm.EndDate);
        }

        [Fact]
        public void ExcludeZeroWages_FiltersOutZeroWageLinesWhenEnabledInMonthlyMode()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedPayType = "MonthlySalary";

            var line1 = new WageRunLine { EmployeeName = "Alice", TotalWage = 5000 };
            var line2 = new WageRunLine { EmployeeName = "Bob", TotalWage = 0 };

            vm.Lines.Add(new WageRunLineViewModel(line1));
            vm.Lines.Add(new WageRunLineViewModel(line2));

            // Act 1: Toggle disabled (default)
            vm.ExcludeZeroWages = false;
            var visibleLinesDefault = vm.LinesView.Cast<WageRunLineViewModel>().ToList();

            // Assert 1: Both lines present
            Assert.Equal(2, visibleLinesDefault.Count);

            // Act 2: Toggle enabled
            vm.ExcludeZeroWages = true;
            var visibleLinesFiltered = vm.LinesView.Cast<WageRunLineViewModel>().ToList();

            // Assert 2: Only Alice (non-zero) present
            Assert.Single(visibleLinesFiltered);
            Assert.Equal("ALICE", visibleLinesFiltered.First().EmployeeName);
        }

        [Fact]
        public async Task EditPastRunAsync_PreservesLoadedRunDatesAndEnablesSaveMode()
        {
            // Arrange
            var vm = CreateViewModel();
            var runId = Guid.NewGuid();
            var expectedStartDate = new DateTime(2026, 7, 25);
            var expectedEndDate = new DateTime(2026, 7, 31);

            var pastRun = new WageRun
            {
                Id = runId,
                StartDate = expectedStartDate,
                EndDate = expectedEndDate,
                Branch = "Johannesburg",
                PayType = "Hourly",
                Status = WageRunStatus.Finalized,
                Lines = new List<WageRunLine>
                {
                    new WageRunLine { Id = Guid.NewGuid(), EmployeeName = "Aaron Moselane", TotalWage = 1973.90m }
                }
            };

            _mockWageService.Setup(w => w.GetWageRunByIdAsync(runId)).ReturnsAsync(pastRun);

            // Act
            await vm.EditPastRunCommand.ExecuteAsync(pastRun);

            // Assert
            Assert.Equal(expectedStartDate, vm.StartDate);
            Assert.Equal(expectedEndDate, vm.EndDate);
            Assert.Equal("Johannesburg", vm.SelectedBranch);
            Assert.True(vm.IsEditingPastRun);
            Assert.Equal("SAVE CHANGES", vm.FinalizeButtonText);
            Assert.Equal("\uE74E", vm.FinalizeButtonIcon);
        }

        [Fact]
        public async Task EditPastRunAsync_UpdatesGrandTotal_WhenLineDeductionIsModified()
        {
            // Arrange
            var vm = CreateViewModel();
            var runId = Guid.NewGuid();
            var pastRun = new WageRun
            {
                Id = runId,
                StartDate = new DateTime(2026, 7, 25),
                EndDate = new DateTime(2026, 7, 31),
                Branch = "Johannesburg",
                PayType = "Hourly",
                Status = WageRunStatus.Finalized,
                Lines = new List<WageRunLine>
                {
                    new WageRunLine
                    {
                        Id = Guid.NewGuid(),
                        EmployeeName = "Andrew Masilela",
                        HourlyRate = 42.40m,
                        NormalHours = 43.75,
                        SaturdayOvertimeHours = 7.0,
                        Overtime20Hours = 5.0,
                        TotalWage = 2724.20m,
                        Branch = "Johannesburg"
                    }
                }
            };

            _mockWageService.Setup(w => w.GetWageRunByIdAsync(runId)).ReturnsAsync(pastRun);
            await vm.EditPastRunCommand.ExecuteAsync(pastRun);

            // Initial Grand Total should equal Andrew's net pay (R 2,724.20)
            Assert.Equal(2724.20m, vm.GrandTotalWage);

            // Act: Add washing deduction (R 1,000.00) to Andrew's line
            var andrewLine = vm.Lines.First();
            andrewLine.DeductionWashing = 1000.00m;

            // Assert: Grand Total should now be R 1,724.20 (2724.20 - 1000.00)
            Assert.Equal(1724.20m, vm.GrandTotalWage);
        }
    }
}
