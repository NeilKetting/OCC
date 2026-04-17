using Moq;
using OCC.WpfClient.Features.HseqHub.ViewModels;
using OCC.WpfClient.Services.Interfaces;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Features.HseqHub
{
    public class IncidentEditorViewModelTests
    {
        private readonly Mock<IHealthSafetyService> _mockHseqService;
        private readonly Mock<IToastService> _mockToastService;
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly IncidentEditorViewModel _vm;

        public IncidentEditorViewModelTests()
        {
            _mockHseqService = new Mock<IHealthSafetyService>();
            _mockToastService = new Mock<IToastService>();
            _mockAuthService = new Mock<IAuthService>();
            
            _vm = new IncidentEditorViewModel(
                _mockHseqService.Object, 
                _mockToastService.Object, 
                _mockAuthService.Object);
        }

        [Fact]
        public void Initialize_SetsState_AndOpens()
        {
            // Arrange
            var incident = new Incident { Description = "Test" };
            var photos = new List<IncidentPhotoDto> { new IncidentPhotoDto { Id = Guid.NewGuid() } };

            // Act
            _vm.Initialize(incident, photos);

            // Assert
            Assert.Equal(incident, _vm.Incident);
            Assert.Single(_vm.Photos);
            Assert.True(_vm.IsOpen);
        }

        [Fact]
        public void Clear_ResetsState_AndCloses()
        {
            // Arrange
            _vm.Initialize(new Incident(), new List<IncidentPhotoDto>());

            // Act
            _vm.Clear();

            // Assert
            Assert.NotNull(_vm.Incident);
            Assert.Empty(_vm.Photos);
            Assert.False(_vm.IsOpen);
        }

        [Fact]
        public async Task SaveIncident_FailsValidation_WhenDescriptionEmpty()
        {
            // Arrange
            _vm.Incident.Description = "";
            _vm.Incident.Location = "Site A";

            // Act
            await _vm.SaveIncidentCommand.ExecuteAsync(null);

            // Assert
            _mockToastService.Verify(s => s.ShowWarning("Validation", It.IsAny<string>()), Times.Once);
            _mockHseqService.Verify(s => s.CreateIncidentAsync(It.IsAny<Incident>()), Times.Never);
        }

        [Fact]
        public async Task SaveIncident_CallsService_AndClosesOnSuccess()
        {
            // Arrange
            _vm.Incident.Description = "Valid Description";
            _vm.Incident.Location = "Valid Location";
            _mockAuthService.SetupGet(a => a.CurrentUser).Returns(new User { Id = Guid.NewGuid() });
            _mockHseqService.Setup(s => s.CreateIncidentAsync(It.IsAny<Incident>())).ReturnsAsync(new IncidentDto { Id = Guid.NewGuid() });

            bool onSavedCalled = false;
            _vm.OnSaved = (summary) => { onSavedCalled = true; return Task.CompletedTask; };

            // Act
            await _vm.SaveIncidentCommand.ExecuteAsync(null);

            // Assert
            _mockHseqService.Verify(s => s.CreateIncidentAsync(It.IsAny<Incident>()), Times.Once);
            _mockToastService.Verify(s => s.ShowSuccess("Success", It.IsAny<string>()), Times.Once);
            Assert.True(onSavedCalled);
            Assert.False(_vm.IsOpen);
        }

        [Fact]
        public async Task DeletePhoto_CallsService_AndRemovesFromCollection()
        {
            // Arrange
            var photo = new IncidentPhotoDto { Id = Guid.NewGuid() };
            _vm.Photos.Add(photo);
            _mockHseqService.Setup(s => s.DeleteIncidentPhotoAsync(photo.Id)).ReturnsAsync(true);

            // Act
            await _vm.DeletePhotoCommand.ExecuteAsync(photo);

            // Assert
            _mockHseqService.Verify(s => s.DeleteIncidentPhotoAsync(photo.Id), Times.Once);
            Assert.Empty(_vm.Photos);
        }
    }
}
