using Moq;
using OCC.WpfClient.Features.HseqHub.ViewModels;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Repositories.Interfaces;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Features.HseqHub
{
    public class AuditDeviationsViewModelTests
    {
        private readonly Mock<IHealthSafetyService> _mockHseqService;
        private readonly Mock<IToastService> _mockToastService;
        private readonly Mock<IRepository<Employee>> _mockEmployeeRepository;
        private readonly Mock<IExportService> _mockExportService;
        private readonly AuditDeviationsViewModel _vm;

        public AuditDeviationsViewModelTests()
        {
            _mockHseqService = new Mock<IHealthSafetyService>();
            _mockToastService = new Mock<IToastService>();
            _mockEmployeeRepository = new Mock<IRepository<Employee>>();
            _mockExportService = new Mock<IExportService>();
            
            _vm = new AuditDeviationsViewModel(
                _mockHseqService.Object, 
                _mockToastService.Object, 
                _mockEmployeeRepository.Object, 
                _mockExportService.Object);
        }

        [Fact]
        public async Task Initialize_LoadsAudit_andSiteManagers()
        {
            // Arrange
            var auditId = Guid.NewGuid();
            var auditDto = new AuditDto
            {
                Id = auditId,
                NonComplianceItems = new List<AuditNonComplianceItemDto>
                {
                    new AuditNonComplianceItemDto { Id = Guid.NewGuid(), Description = "Deviation 1" }
                }
            };

            var employees = new List<Employee>
            {
                new Employee { Id = Guid.NewGuid(), FirstName = "John", Role = EmployeeRole.SiteManager }
            };

            _mockHseqService.Setup(s => s.GetAuditAsync(auditId)).ReturnsAsync(auditDto);
            _mockEmployeeRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(employees);

            // Act
            await _vm.Initialize(auditId);

            // Assert
            Assert.NotNull(_vm.SelectedAudit);
            Assert.Equal(auditId, _vm.SelectedAudit!.Id);
            Assert.Single(_vm.Deviations);
            Assert.Single(_vm.SiteManagers);
            Assert.Equal("John", _vm.SiteManagers[0].FirstName);
        }

        [Fact]
        public void AddDeviation_AddsToCollections()
        {
            // Arrange
            _vm.SelectedAudit = new HseqAudit { Id = Guid.NewGuid() };

            // Act
            _vm.AddDeviationCommand.Execute(null);

            // Assert
            Assert.Single(_vm.Deviations);
            Assert.Single(_vm.SelectedAudit.NonComplianceItems);
        }

        [Fact]
        public async Task SaveDeviation_CommitsAndUpdatesAudit()
        {
            // Arrange
            var auditId = Guid.NewGuid();
            _vm.SelectedAudit = new HseqAudit { Id = auditId };
            _vm.AddDeviationCommand.Execute(null);
            var wrapper = _vm.Deviations[0];
            wrapper.Description = "Updated Description";

            _mockHseqService.Setup(s => s.UpdateAuditAsync(It.IsAny<AuditDto>())).ReturnsAsync(true);

            // Act
            await _vm.SaveDeviationCommand.ExecuteAsync(wrapper);

            // Assert
            _mockHseqService.Verify(s => s.UpdateAuditAsync(It.Is<AuditDto>(d => d.NonComplianceItems.Any(i => i.Description == "Updated Description"))), Times.Once);
            _mockToastService.Verify(s => s.ShowSuccess("Saved", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task GenerateCloseOutReport_CallsExportService()
        {
            // Arrange
            _vm.SelectedAudit = new HseqAudit { Id = Guid.NewGuid() };
            _mockExportService.Setup(e => e.GenerateAuditDeviationReportAsync(It.IsAny<HseqAudit>(), It.IsAny<IEnumerable<HseqAuditNonComplianceItem>>()))
                .ReturnsAsync("path/to/report.pdf");

            // Act
            await _vm.GenerateCloseOutReportCommand.ExecuteAsync(null);

            // Assert
            _mockExportService.Verify(e => e.GenerateAuditDeviationReportAsync(_vm.SelectedAudit, It.IsAny<IEnumerable<HseqAuditNonComplianceItem>>()), Times.Once);
            _mockExportService.Verify(e => e.OpenFileAsync("path/to/report.pdf"), Times.Once);
        }
    }
}
