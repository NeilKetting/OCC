using Microsoft.Extensions.Logging;
using Moq;
using OCC.Shared.Models;
using OCC.WpfClient.Features.AttendanceHub.ViewModels;
using OCC.WpfClient.Services.Interfaces;
using System;
using Xunit;

namespace OCC.Tests.Features.AttendanceHub
{
    /// <summary>
    /// Unit tests for <see cref="LeaveManagementViewModel"/>.
    /// </summary>
    public class LeaveManagementViewModelTests
    {
        private readonly Mock<ILeaveService> _mockLeaveService;
        private readonly Mock<IEmployeeService> _mockEmployeeService;
        private readonly Mock<IAttendanceService> _mockAttendanceService;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly Mock<IPdfService> _mockPdfService;
        private readonly Mock<ILogger<LeaveManagementViewModel>> _mockLogger;

        public LeaveManagementViewModelTests()
        {
            _mockLeaveService = new Mock<ILeaveService>();
            _mockEmployeeService = new Mock<IEmployeeService>();
            _mockAttendanceService = new Mock<IAttendanceService>();
            _mockDialogService = new Mock<IDialogService>();
            _mockPdfService = new Mock<IPdfService>();
            _mockLogger = new Mock<ILogger<LeaveManagementViewModel>>();
        }

        private LeaveManagementViewModel CreateViewModel()
        {
            return new LeaveManagementViewModel(
                _mockLeaveService.Object,
                _mockEmployeeService.Object,
                _mockAttendanceService.Object,
                _mockDialogService.Object,
                _mockPdfService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public void SelectedLeaveType_SetToHalfDay_SyncsEndDateToStartDate_AndCalculatesHalfDay()
        {
            // Arrange
            var vm = CreateViewModel();
            var targetDate = new DateTime(2026, 7, 28);

            // Act
            vm.SelectedLeaveType = LeaveType.HalfDay;
            vm.StartDate = targetDate;

            // Assert
            Assert.True(vm.IsHalfDayType);
            Assert.Equal(targetDate, vm.EndDate);
            Assert.Equal(0.5, vm.CalculatedDays);
        }

        [Fact]
        public void SelectedLeaveType_SwitchedFromHalfDayToAnnual_ResetsIsUnpaidToFalse()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedLeaveType = LeaveType.HalfDay;
            vm.IsUnpaid = true;

            // Act
            vm.SelectedLeaveType = LeaveType.Annual;

            // Assert
            Assert.False(vm.IsHalfDayType);
            Assert.False(vm.IsUnpaid);
        }
    }
}
