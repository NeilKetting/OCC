using Moq;
using OCC.WpfClient.Features.HseqHub.ViewModels;
using OCC.WpfClient.ModelWrappers;
using OCC.WpfClient.Services.Interfaces;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Features.HseqHub
{
    public class AuditEditorViewModelTests
    {
        private readonly Mock<IHealthSafetyService> _mockHseqService;
        private readonly Mock<IToastService> _mockToastService;
        private readonly AuditEditorViewModel _vm;

        public AuditEditorViewModelTests()
        {
            _mockHseqService = new Mock<IHealthSafetyService>();
            _mockToastService = new Mock<IToastService>();
            _vm = new AuditEditorViewModel(_mockHseqService.Object, _mockToastService.Object);
        }

        [Fact]
        public void InitializeForNew_SetsDefaultState()
        {
            // Act
            _vm.InitializeForNew();

            // Assert
            Assert.Equal("New Audit", _vm.Title);
            Assert.NotNull(_vm.CurrentAudit);
            Assert.Equal(DateTime.Today, _vm.CurrentAudit.Date);
            Assert.Equal(10, _vm.CurrentAudit.Sections.Count);
            Assert.Empty(_vm.Findings);
        }

        [Fact]
        public async Task InitializeForEdit_LoadsAudit_AndPopulatesFindings()
        {
            // Arrange
            var auditId = Guid.NewGuid();
            var auditDto = new AuditDto
            {
                Id = auditId,
                Date = DateTime.Now,
                Sections = new List<AuditSectionDto>(),
                NonComplianceItems = new List<AuditNonComplianceItemDto>
                {
                    new AuditNonComplianceItemDto { Id = Guid.NewGuid(), Description = "Test Finding" }
                },
                Attachments = new List<AuditAttachmentDto>()
            };

            _mockHseqService.Setup(s => s.GetAuditAsync(auditId)).ReturnsAsync(auditDto);

            // Act
            await _vm.InitializeForEdit(auditId);

            // Assert
            Assert.Equal(auditId, _vm.CurrentAudit.Id);
            Assert.Single(_vm.Findings);
            Assert.Equal("Test Finding", _vm.Findings[0].Description);
        }

        [Fact]
        public async Task Save_CalculatesScore_AndCallsUpdate_WhenExisting()
        {
            // Arrange
            _vm.InitializeForNew();
            _vm.CurrentAudit.Id = Guid.NewGuid(); // Simulate existing
            _vm.CurrentAudit.Sections.Clear();
            _vm.CurrentAudit.Sections.Add(new HseqAuditSection { PossibleScore = 100, ActualScore = 80 });
            
            _mockHseqService.Setup(s => s.UpdateAuditAsync(It.IsAny<AuditDto>())).ReturnsAsync(true);

            // Act
            await _vm.SaveCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(80, _vm.CurrentAudit.ActualScore);
            _mockHseqService.Verify(s => s.UpdateAuditAsync(It.Is<AuditDto>(d => d.ActualScore == 80)), Times.Once);
            _mockToastService.Verify(s => s.ShowSuccess("Saved", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void AddFinding_AddsToCollections()
        {
            // Arrange
            _vm.InitializeForNew();

            // Act
            _vm.AddFindingCommand.Execute(null);

            // Assert
            Assert.Single(_vm.Findings);
            Assert.Single(_vm.CurrentAudit.NonComplianceItems);
        }

        [Fact]
        public void DeleteFinding_RemovesFromCollections()
        {
            // Arrange
            _vm.InitializeForNew();
            _vm.AddFindingCommand.Execute(null);
            var finding = _vm.CurrentAudit.NonComplianceItems[0];

            // Act
            _vm.DeleteFindingCommand.Execute(finding);

            // Assert
            Assert.Empty(_vm.Findings);
            Assert.Empty(_vm.CurrentAudit.NonComplianceItems);
        }
    }
}
