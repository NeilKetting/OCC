using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Services;
using Xunit;

namespace OCC.Tests
{
    public class BankExportServiceTests
    {
        private readonly ExportService _service;

        public BankExportServiceTests()
        {
            _service = new ExportService();
        }

        [Fact]
        public async Task GenerateBankExportFileAsync_StandardCsv_FormatsCorrectly()
        {
            // Arrange
            var payments = new List<BankPaymentDto>
            {
                new()
                {
                    EmployeeName = "John Doe",
                    EmployeeNumber = "EMP001",
                    BankName = "Nedbank",
                    AccountNumber = "123456789",
                    BranchCode = "198765",
                    AccountType = "Savings",
                    Amount = 1500.50m,
                    Reference = "Wage 20260710"
                },
                new()
                {
                    EmployeeName = "Jane Smith, Jr.",
                    EmployeeNumber = "EMP002",
                    BankName = "FNB",
                    AccountNumber = "987654321",
                    BranchCode = "250655",
                    AccountType = "Cheque",
                    Amount = 2500.00m,
                    Reference = "Wage 20260710"
                }
            };

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");

            try
            {
                // Act
                var resultPath = await _service.GenerateBankExportFileAsync(payments, BankFormat.StandardCsv, new DateTime(2026, 7, 10), tempPath);

                // Assert
                Assert.True(File.Exists(resultPath));
                var lines = await File.ReadAllLinesAsync(resultPath);
                
                Assert.Equal(3, lines.Length);
                Assert.Equal("Beneficiary Name,Employee Number,Bank Name,Account Number,Branch Code,Account Type,Amount,Payment Reference,Action Date", lines[0]);
                Assert.Equal("John Doe,EMP001,Nedbank,123456789,198765,Savings,1500.50,Wage 20260710,2026-07-10", lines[1]);
                // Check escaping of comma in "Jane Smith, Jr."
                Assert.Equal("\"Jane Smith, Jr.\",EMP002,FNB,987654321,250655,Cheque,2500.00,Wage 20260710,2026-07-10", lines[2]);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public async Task GenerateBankExportFileAsync_NedbankNetBankCsv_FormatsCorrectly()
        {
            // Arrange
            var payments = new List<BankPaymentDto>
            {
                new()
                {
                    EmployeeName = "John Doe",
                    EmployeeNumber = "EMP001",
                    BankName = "Nedbank",
                    AccountNumber = "123456789",
                    BranchCode = "198765",
                    AccountType = "Savings",
                    Amount = 1500.50m,
                    Reference = "Wage 20260710"
                }
            };

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");

            try
            {
                // Act
                var resultPath = await _service.GenerateBankExportFileAsync(payments, BankFormat.NedbankNetBankCsv, new DateTime(2026, 7, 10), tempPath);

                // Assert
                Assert.True(File.Exists(resultPath));
                var lines = await File.ReadAllLinesAsync(resultPath);
                
                Assert.Equal(2, lines.Length);
                Assert.Equal("Record Type,Beneficiary Name,Beneficiary Account Number,Branch Code,Account Type,Amount,Your Reference,Their Reference,Action Date", lines[0]);
                Assert.Equal("PAY,John Doe,123456789,198765,2,1500.50,OCC WAGES,WAGES 20260710,2026-07-10", lines[1]);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }
}
