using Moq;
using OCC.WpfClient.Features.HseqHub.ViewModels;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Repositories.Interfaces;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Features.HseqHub
{
    public class TrainingEditorViewModelTests
    {
        private readonly Mock<IHealthSafetyService> _mockHseqService;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly Mock<IToastService> _mockToastService;
        private readonly Mock<IRepository<Employee>> _mockEmployeeRepository;
        private readonly TrainingEditorViewModel _vm;

        public TrainingEditorViewModelTests()
        {
            _mockHseqService = new Mock<IHealthSafetyService>();
            _mockDialogService = new Mock<IDialogService>();
            _mockToastService = new Mock<IToastService>();
            _mockEmployeeRepository = new Mock<IRepository<Employee>>();
            
            _vm = new TrainingEditorViewModel(
                _mockHseqService.Object, 
                _mockDialogService.Object, 
                _mockToastService.Object, 
                _mockEmployeeRepository.Object);
        }

        [Fact]
        public void Initialize_SetsEmployees_AndClearsForm()
        {
            // Arrange
            var employees = new List<Employee> { new Employee { Id = Guid.NewGuid(), FirstName = "Neil" } };

            // Act
            _vm.Initialize(employees, new List<string>());

            // Assert
            Assert.Single(_vm.Employees);
            Assert.False(_vm.IsOpen);
            Assert.Equal("No file selected", _vm.CertificateFileName);
        }

        [Fact]
        public void OpenForAdd_Opens_AndSetsDefaults()
        {
            // Act
            _vm.OpenForAdd();

            // Assert
            Assert.True(_vm.IsOpen);
            Assert.False(_vm.IsEditMode);
            Assert.Equal(DateTime.Today, _vm.NewRecord.DateCompleted.Date);
        }

        [Fact]
        public void OpenForEdit_SetsState_FromRecord()
        {
            // Arrange
            var empId = Guid.NewGuid();
            var employees = new List<Employee> { new Employee { Id = empId, FirstName = "Neil" } };
            var record = new HseqTrainingRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = empId,
                EmployeeName = "Neil Ketting",
                CertificateType = "First Aid"
            };

            // Act
            _vm.OpenForEdit(record, employees);

            // Assert
            Assert.True(_vm.IsOpen);
            Assert.True(_vm.IsEditMode);
            Assert.Equal("Neil Ketting", _vm.NewRecord.EmployeeName);
            Assert.NotNull(_vm.SelectedEmployee);
            Assert.Equal(empId, _vm.SelectedEmployee!.Id);
        }

        [Fact]
        public void SelectingEmployee_UpdatesRecordDetails()
        {
            // Arrange
            _vm.OpenForAdd();
            var employee = new Employee { Id = Guid.NewGuid(), FirstName = "Neil", LastName = "Ketting", Role = EmployeeRole.SiteManager };

            // Act
            _vm.SelectedEmployee = employee;

            // Assert
            Assert.Equal("Neil, Ketting", _vm.NewRecord.EmployeeName);
            Assert.Equal(EmployeeRole.SiteManager.ToString(), _vm.NewRecord.Role);
            Assert.Equal(employee.Id, _vm.NewRecord.EmployeeId);
        }

        [Fact]
        public async Task SaveTraining_FailsValidation_WhenRequiredFieldsMissing()
        {
            // Arrange
            _vm.OpenForAdd();
            _vm.NewRecord.EmployeeName = "";

            // Act
            await _vm.SaveTrainingCommand.ExecuteAsync(null);

            // Assert
            _mockToastService.Verify(s => s.ShowError("Validation", It.IsAny<string>()), Times.Once);
            _mockHseqService.Verify(s => s.CreateTrainingRecordAsync(It.IsAny<HseqTrainingRecord>()), Times.Never);
        }

        [Fact]
        public async Task SaveTraining_CallsCreate_ForNewRecord()
        {
            // Arrange
            _vm.OpenForAdd();
            _vm.NewRecord.EmployeeName = "Neil";
            _vm.NewRecord.CertificateType = "Medicals";
            _mockHseqService.Setup(s => s.CreateTrainingRecordAsync(It.IsAny<HseqTrainingRecord>())).ReturnsAsync(new HseqTrainingRecord { Id = Guid.NewGuid() });

            bool onSavedCalled = false;
            _vm.OnSaved = (r) => { onSavedCalled = true; return Task.CompletedTask; };

            // Act
            await _vm.SaveTrainingCommand.ExecuteAsync(null);

            // Assert
            _mockHseqService.Verify(s => s.CreateTrainingRecordAsync(It.Is<HseqTrainingRecord>(r => r.CertificateType == "Medicals")), Times.Once);
            _mockToastService.Verify(s => s.ShowSuccess("Saved", It.IsAny<string>()), Times.Once);
            Assert.True(onSavedCalled);
            Assert.False(_vm.IsOpen);
        }
    }
}
