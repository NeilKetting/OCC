using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Features.Main.ViewModels;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using Xunit;

namespace OCC.Tests.Features.Main
{
    public class AlertsWidgetViewModelTests
    {
        [Fact]
        public async Task RefreshDataAsync_GroupsAlertsCorrectlyWhenPermissionGranted()
        {
            // Arrange
            var mockEmployee = new Mock<IEmployeeService>();
            var mockPermission = new Mock<IPermissionService>();
            var mockToast = new Mock<IToastService>();

            var localSettings = new LocalSettingsService(
                NullLogger<LocalSettingsService>.Instance,
                mockToast.Object);

            localSettings.Settings.ActionCenterTrackPassportAlerts = true;
            localSettings.Settings.ActionCenterTrackBankingAlerts = true;

            var empExpiredId = Guid.NewGuid();
            var empExpiringSoonId = Guid.NewGuid();
            var empMissingBankId = Guid.NewGuid();

            var employees = new List<EmployeeSummaryDto>
            {
                new EmployeeSummaryDto
                {
                    Id = empExpiredId,
                    FirstName = "Donald",
                    LastName = "Jiya",
                    Status = EmployeeStatus.Active,
                    IdType = IdType.Passport,
                    PassportStampDate = DateTime.Today.AddDays(-120), // Expired
                    BankAccountNumber = "123456",
                    BankName = "FNB"
                },
                new EmployeeSummaryDto
                {
                    Id = empExpiringSoonId,
                    FirstName = "Stuart",
                    LastName = "Khoza",
                    Status = EmployeeStatus.Active,
                    IdType = IdType.Passport,
                    PassportStampDate = DateTime.Today.AddDays(-40), // Expiring soon
                    BankAccountNumber = "654321",
                    BankName = "Standard Bank"
                },
                new EmployeeSummaryDto
                {
                    Id = empMissingBankId,
                    FirstName = "Professor",
                    LastName = "Mbedzi",
                    Status = EmployeeStatus.Active,
                    IdType = IdType.RSAId,
                    BankAccountNumber = "",
                    BankName = ""
                }
            };

            mockEmployee.Setup(s => s.GetEmployeesAsync()).ReturnsAsync(employees);
            mockPermission.Setup(s => s.CanAccess(It.IsAny<string>())).Returns(true);

            var vm = new AlertsWidgetViewModel(mockEmployee.Object, mockPermission.Object, localSettings);
            vm.TrackPassportAlerts = true;
            vm.TrackBankingAlerts = true;

            // Act
            await vm.RefreshDataAsync();

            // Assert
            Assert.True(vm.CanAccessStaffManagement);
            Assert.Equal(3, vm.AlertCount);
            Assert.Equal(3, vm.AlertGroups.Count);

            var expiredGroup = vm.AlertGroups.FirstOrDefault(g => g.CategoryType == "PassportExpired");
            Assert.NotNull(expiredGroup);
            Assert.Single(expiredGroup.Items);
            Assert.Equal("Donald Jiya", expiredGroup.Items.First().Title);

            var expiringGroup = vm.AlertGroups.FirstOrDefault(g => g.CategoryType == "PassportExpiringSoon");
            Assert.NotNull(expiringGroup);
            Assert.Single(expiringGroup.Items);
            Assert.Equal("Stuart Khoza", expiringGroup.Items.First().Title);

            var bankingGroup = vm.AlertGroups.FirstOrDefault(g => g.CategoryType == "Banking");
            Assert.NotNull(bankingGroup);
            Assert.Single(bankingGroup.Items);
            Assert.Equal("Professor Mbedzi", bankingGroup.Items.First().Title);
        }

        [Fact]
        public async Task RefreshDataAsync_HidesAlertsWhenPermissionDenied()
        {
            // Arrange
            var mockEmployee = new Mock<IEmployeeService>();
            var mockPermission = new Mock<IPermissionService>();
            var mockToast = new Mock<IToastService>();

            var localSettings = new LocalSettingsService(
                NullLogger<LocalSettingsService>.Instance,
                mockToast.Object);

            mockPermission.Setup(s => s.CanAccess(It.IsAny<string>())).Returns(false);

            var vm = new AlertsWidgetViewModel(mockEmployee.Object, mockPermission.Object, localSettings);

            // Act
            await vm.RefreshDataAsync();

            // Assert
            Assert.False(vm.CanAccessStaffManagement);
            Assert.Equal(0, vm.AlertCount);
            Assert.Empty(vm.AlertGroups);
        }

        [Fact]
        public async Task RefreshDataAsync_FiltersOutDisabledAlertCategories()
        {
            // Arrange
            var mockEmployee = new Mock<IEmployeeService>();
            var mockPermission = new Mock<IPermissionService>();
            var mockToast = new Mock<IToastService>();

            var localSettings = new LocalSettingsService(
                NullLogger<LocalSettingsService>.Instance,
                mockToast.Object);

            localSettings.Settings.ActionCenterTrackPassportAlerts = true;
            localSettings.Settings.ActionCenterTrackBankingAlerts = false;

            var employees = new List<EmployeeSummaryDto>
            {
                new EmployeeSummaryDto
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Donald",
                    LastName = "Jiya",
                    Status = EmployeeStatus.Active,
                    IdType = IdType.Passport,
                    PassportStampDate = DateTime.Today.AddDays(-120),
                    BankAccountNumber = "123456",
                    BankName = "FNB"
                },
                new EmployeeSummaryDto
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Professor",
                    LastName = "Mbedzi",
                    Status = EmployeeStatus.Active,
                    IdType = IdType.RSAId,
                    BankAccountNumber = "",
                    BankName = ""
                }
            };

            mockEmployee.Setup(s => s.GetEmployeesAsync()).ReturnsAsync(employees);
            mockPermission.Setup(s => s.CanAccess(It.IsAny<string>())).Returns(true);

            var vm = new AlertsWidgetViewModel(mockEmployee.Object, mockPermission.Object, localSettings);
            vm.TrackPassportAlerts = true;
            vm.TrackBankingAlerts = false;

            // Act
            await vm.RefreshDataAsync();

            // Assert
            Assert.Single(vm.AlertGroups);
            Assert.DoesNotContain(vm.AlertGroups, g => g.CategoryType == "Banking");
            Assert.Contains(vm.AlertGroups, g => g.CategoryType == "PassportExpired");
        }

        [Fact]
        public void ToggleSettings_TogglesIsSettingsOpen()
        {
            // Arrange
            var mockEmployee = new Mock<IEmployeeService>();
            var mockPermission = new Mock<IPermissionService>();
            var mockToast = new Mock<IToastService>();

            var localSettings = new LocalSettingsService(
                NullLogger<LocalSettingsService>.Instance,
                mockToast.Object);

            var vm = new AlertsWidgetViewModel(mockEmployee.Object, mockPermission.Object, localSettings);

            Assert.False(vm.IsSettingsOpen);

            // Act & Assert
            vm.ToggleSettingsCommand.Execute(null);
            Assert.True(vm.IsSettingsOpen);

            vm.ToggleSettingsCommand.Execute(null);
            Assert.False(vm.IsSettingsOpen);
        }
    }
}
