using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class EmployeeLoansControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<EmployeeLoansController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;

        public EmployeeLoansControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<EmployeeLoansController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
        }

        [Fact]
        public async Task GetLoanStatement_SuccessScenario()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new EmployeeLoansController(context, _mockLogger.Object, _mockHubContext.Object);

            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Doe",
                EmployeeNumber = "EMP001"
            };

            var loan = new EmployeeLoan
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                Employee = employee,
                PrincipalAmount = 1000,
                MonthlyInstallment = 100,
                OutstandingBalance = 1000,
                InterestRate = 10,
                StartDate = DateTime.Today.AddMonths(-1),
                IsActive = true
            };

            var wageRun = new WageRun
            {
                Id = Guid.NewGuid(),
                StartDate = DateTime.Today.AddDays(-10),
                EndDate = DateTime.Today.AddDays(-3),
                RunDate = DateTime.Today.AddDays(-2),
                Status = WageRunStatus.Finalized
            };

            var wageRunLine = new WageRunLine
            {
                Id = Guid.NewGuid(),
                WageRunId = wageRun.Id,
                WageRun = wageRun,
                EmployeeId = employee.Id,
                DeductionLoan = 100,
                EmployeeName = "John Doe",
                EmployeeNumber = "EMP001"
            };

            context.Employees.Add(employee);
            context.EmployeeLoans.Add(loan);
            context.WageRuns.Add(wageRun);
            context.WageRunLines.Add(wageRunLine);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetLoanStatement(loan.Id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var statement = Assert.IsType<LoanStatementDto>(okResult.Value);
            Assert.Equal(loan.Id, statement.LoanId);
            Assert.Single(statement.Payments);
            Assert.Equal(100, statement.Payments[0].Amount);
        }
    }
}
