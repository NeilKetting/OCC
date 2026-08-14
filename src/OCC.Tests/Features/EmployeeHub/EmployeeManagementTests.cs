using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.WpfClient.Features.EmployeeHub.Models;
using OCC.WpfClient.Features.EmployeeHub.ViewModels;
using Xunit;

namespace OCC.Tests.Features.EmployeeHub
{
    public class EmployeeManagementTests
    {
        [Fact]
        public void EmployeeModel_EmploymentDateChanged_SyncsLeaveCycleStartDate()
        {
            // Arrange
            var model = new EmployeeModel();
            var newDate = new DateTime(2024, 6, 15);

            // Act
            model.EmploymentDate = newDate;

            // Assert
            Assert.Equal(newDate, model.LeaveCycleStartDate);
        }

        [Fact]
        public void EmployeeModel_UpdateFromEntity_NullLeaveCycleStartDate_DefaultsToEmploymentDate()
        {
            // Arrange
            var dto = new EmployeeDto
            {
                Id = Guid.NewGuid(),
                FirstName = "Test",
                LastName = "User",
                EmploymentDate = new DateTime(2023, 1, 10),
                LeaveCycleStartDate = null
            };

            // Act
            var model = new EmployeeModel(dto);

            // Assert
            Assert.Equal(new DateTime(2023, 1, 10), model.LeaveCycleStartDate);
        }

        [Fact]
        public void EmployeeDetailViewModel_CalculateDoBFromRsaId_ExtractsCorrect19yyAnd20yyDates()
        {
            // Arrange & Act - Test 1992 birth year (920512)
            var emp1 = new EmployeeModel { IdNumber = "9205125088081" };
            var vm1 = new EmployeeDetailViewModel(
                emp1,
                new Mock<OCC.WpfClient.Services.Interfaces.IEmployeeService>().Object,
                new Mock<OCC.WpfClient.Services.Interfaces.IUserService>().Object,
                new Mock<OCC.WpfClient.Services.Interfaces.IAuthService>().Object,
                new Mock<OCC.WpfClient.Services.Interfaces.IDialogService>().Object,
                new NullLogger<EmployeeDetailViewModel>(),
                new Mock<OCC.WpfClient.Services.Interfaces.IPdfService>().Object
            );

            Assert.Equal(new DateTime(1992, 5, 12), emp1.DoB);

            // Arrange & Act - Test 2005 birth year (050315)
            var emp2 = new EmployeeModel { IdNumber = "0503155088081" };
            var vm2 = new EmployeeDetailViewModel(
                emp2,
                new Mock<OCC.WpfClient.Services.Interfaces.IEmployeeService>().Object,
                new Mock<OCC.WpfClient.Services.Interfaces.IUserService>().Object,
                new Mock<OCC.WpfClient.Services.Interfaces.IAuthService>().Object,
                new Mock<OCC.WpfClient.Services.Interfaces.IDialogService>().Object,
                new NullLogger<EmployeeDetailViewModel>(),
                new Mock<OCC.WpfClient.Services.Interfaces.IPdfService>().Object
            );

            Assert.Equal(new DateTime(2005, 3, 15), emp2.DoB);
        }

        [Fact]
        public async Task EmployeesController_PutEmployee_PreservesExistingFieldsOnPartialUpdate()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using (var context = new AppDbContext(options))
            {
                var empId = Guid.NewGuid();
                var originalEmp = new Employee
                {
                    Id = empId,
                    FirstName = "InitialFirstName",
                    LastName = "InitialLastName",
                    IdNumber = "9001015088081",
                    EmploymentDate = new DateTime(2020, 5, 1),
                    LeaveCycleStartDate = new DateTime(2020, 5, 1),
                    DoB = new DateTime(1990, 1, 1),
                    TaxNumber = "TAX123456",
                    BankName = "FNB",
                    AccountNumber = "62000000000",
                    SickLeaveBalance = 30,
                    AnnualLeaveBalance = 15
                };
                context.Employees.Add(originalEmp);
                await context.SaveChangesAsync();

                var mockHub = new Mock<IHubContext<NotificationHub>>();
                var mockClients = new Mock<IHubClients>();
                var mockClientProxy = new Mock<IClientProxy>();
                mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
                mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

                var controller = new EmployeesController(context, new NullLogger<EmployeesController>(), mockHub.Object);

                // Act: Partial update payload with default/null fields (e.g. balance deduction update)
                var partialUpdate = new Employee
                {
                    Id = empId,
                    FirstName = "",
                    LastName = "",
                    IdNumber = "",
                    EmploymentDate = default,
                    LeaveCycleStartDate = null,
                    DoB = default,
                    TaxNumber = "",
                    BankName = "",
                    AccountNumber = "",
                    SickLeaveBalance = 29,
                    AnnualLeaveBalance = 15
                };

                var result = await controller.PutEmployee(empId, partialUpdate);

                // Assert
                Assert.IsType<NoContentResult>(result);

                var dbEmp = await context.Employees.FindAsync(empId);
                Assert.NotNull(dbEmp);
                Assert.Equal("InitialFirstName", dbEmp.FirstName);
                Assert.Equal("InitialLastName", dbEmp.LastName);
                Assert.Equal("9001015088081", dbEmp.IdNumber);
                Assert.Equal(new DateTime(2020, 5, 1), dbEmp.EmploymentDate);
                Assert.Equal(new DateTime(2020, 5, 1), dbEmp.LeaveCycleStartDate);
                Assert.Equal(new DateTime(1990, 1, 1), dbEmp.DoB);
                Assert.Equal("TAX123456", dbEmp.TaxNumber);
                Assert.Equal("FNB", dbEmp.BankName);
                Assert.Equal("62000000000", dbEmp.AccountNumber);
                Assert.Equal(29, dbEmp.SickLeaveBalance);
            }
        }
    }
}
