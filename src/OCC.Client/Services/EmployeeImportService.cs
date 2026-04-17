using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.Client.Services.Repositories.Interfaces;

namespace OCC.Client.Services
{
    public class EmployeeImportService : IEmployeeImportService
    {
        private readonly IRepository<Employee> _employeeRepository;
        private readonly ILogger<EmployeeImportService> _logger;

        public EmployeeImportService(IRepository<Employee> employeeRepository, ILogger<EmployeeImportService> logger)
        {
            _employeeRepository = employeeRepository;
            _logger = logger;
        }

        public async Task<(int SuccessCount, int FailureCount, List<string> Errors)> ImportEmployeesAsync(Stream csvStream)
        {
            var successCount = 0;
            var failureCount = 0;
            var errors = new List<string>();

            try
            {
                using var reader = new StreamReader(csvStream);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null,
                    PrepareHeaderForMatch = args => args.Header.Trim(),
                    TrimOptions = TrimOptions.Trim,
                });

                var records = csv.GetRecords<EmployeeImportRow>();

                var existingEmployees = await _employeeRepository.GetAllAsync();
                var existingIdNumbers = existingEmployees.Where(e => !string.IsNullOrEmpty(e.IdNumber))
                                                         .Select(e => e.IdNumber.Trim().ToLower())
                                                         .ToHashSet();
                var existingEmpNumbers = existingEmployees.Where(e => !string.IsNullOrEmpty(e.EmployeeNumber))
                                                          .Select(e => e.EmployeeNumber.Trim().ToLower())
                                                          .ToHashSet();

                foreach (var row in records)
                {
                    if (string.IsNullOrWhiteSpace(row.FirstName) || string.IsNullOrWhiteSpace(row.LastName))
                    {
                         continue; // Skip empty rows
                    }

                    // Duplicate Check: ID Number
                    if (!string.IsNullOrWhiteSpace(row.IdNumber) && existingIdNumbers.Contains(row.IdNumber.Trim().ToLower()))
                    {
                        errors.Add($"Skipped '{row.FirstName} {row.LastName}' - ID Number {row.IdNumber} already exists.");
                        failureCount++;
                        continue;
                    }

                    // Duplicate Check: Employee Number
                    if (!string.IsNullOrWhiteSpace(row.EmployeeNumber) && existingEmpNumbers.Contains(row.EmployeeNumber.Trim().ToLower()))
                    {
                        errors.Add($"Skipped '{row.FirstName} {row.LastName}' - Employee Number {row.EmployeeNumber} already exists.");
                        failureCount++;
                        continue;
                    }

                    try
                    {
                        var employee = MapToEmployee(row);
                        
                        if (!string.IsNullOrWhiteSpace(employee.IdNumber)) existingIdNumbers.Add(employee.IdNumber.ToLower());
                        if (!string.IsNullOrWhiteSpace(employee.EmployeeNumber)) existingEmpNumbers.Add(employee.EmployeeNumber.ToLower());

                        await _employeeRepository.AddAsync(employee);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import employee {Name}", $"{row.FirstName} {row.LastName}");
                        errors.Add($"Failed to import '{row.FirstName} {row.LastName}': {ex.Message}");
                        failureCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error reading CSV");
                errors.Add($"Fatal error reading CSV: {ex.Message}");
            }

            return (successCount, failureCount, errors);
        }

        private Employee MapToEmployee(EmployeeImportRow row)
        {
            // Enums
            if (!Enum.TryParse<EmployeeRole>(row.Role?.Replace(" ", ""), true, out var role)) role = EmployeeRole.GeneralWorker;
            if (!Enum.TryParse<EmploymentType>(row.EmploymentType?.Trim(), true, out var empType)) empType = EmploymentType.Permanent;
            if (!Enum.TryParse<RateType>(row.RateType?.Replace(" ", "").Replace("FixedSalary", "MonthlySalary"), true, out var rateType)) rateType = RateType.Hourly;
            if (!Enum.TryParse<IdType>(row.IdType?.Replace(" ", ""), true, out var idType)) idType = IdType.RSAId;

            // Dates
            DateTime dob = DateTime.Now.AddYears(-30); // Default if not provided
            if (DateTime.TryParse(row.DoB, out var d)) dob = d;

            DateTime employmentDate = DateTime.Now;
            if(DateTime.TryParse(row.EmploymentDate, out var ed)) employmentDate = ed;

            DateTime? leaveStartDate = null;
            if(DateTime.TryParse(row.LeaveCycleStartDate, out var lsd)) leaveStartDate = lsd;

            // Times
            TimeSpan shiftStart = new TimeSpan(7, 0, 0);
            if (TimeSpan.TryParse(row.ShiftStartTime, out var ss)) shiftStart = ss;

            TimeSpan shiftEnd = new TimeSpan(16, 45, 0);
            if (TimeSpan.TryParse(row.ShiftEndTime, out var se)) shiftEnd = se;

            return new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = row.EmployeeNumber?.Trim() ?? string.Empty,
                FirstName = row.FirstName?.Trim() ?? string.Empty,
                LastName = row.LastName?.Trim() ?? string.Empty,
                Phone = row.Phone?.Trim() ?? string.Empty,
                Email = row.Email?.Trim() ?? string.Empty,
                IdType = idType,
                IdNumber = row.IdNumber?.Trim() ?? string.Empty,
                PermitNumber = row.PermitNumber?.Trim(),
                TaxNumber = row.TaxNumber?.Trim() ?? string.Empty,
                EmploymentDate = employmentDate,
                EmploymentType = empType,
                ContractDuration = row.ContractDuration,
                Role = role,
                RateType = rateType,
                HourlyRate = row.Rate,
                Branch = row.Branch?.Trim() ?? "Johannesburg",
                ShiftStartTime = shiftStart,
                ShiftEndTime = shiftEnd,
                BankName = row.BankName?.Trim(),
                AccountType = row.AccountType?.Trim(),
                AccountNumber = row.AccountNumber?.Trim(),
                BranchCode = row.BranchCode?.Trim(),
                AnnualLeaveBalance = row.AnnualLeaveBalance,
                SickLeaveBalance = row.SickLeaveBalance,
                LeaveCycleStartDate = leaveStartDate,
                Status = EmployeeStatus.Active
            };
        }

        private class EmployeeImportRow
        {
            [Name("Employee Number")]
            public string EmployeeNumber { get; set; } = string.Empty;
            [Name("First Name")]
            public string FirstName { get; set; } = string.Empty;
            [Name("Last Name")]
            public string LastName { get; set; } = string.Empty;
            [Name("Phone Number")]
            public string Phone { get; set; } = string.Empty;
            [Name("Email Address")]
            public string Email { get; set; } = string.Empty;
            [Name("ID Type")]
            public string IdType { get; set; } = string.Empty;
            [Name("ID Number")]
            public string IdNumber { get; set; } = string.Empty;
            [Name("Permit Number")]
            public string? PermitNumber { get; set; }
            [Name("Tax Number")]
            public string TaxNumber { get; set; } = string.Empty;
            [Name("Employment Date")]
            public string EmploymentDate { get; set; } = string.Empty;
            [Name("Employment Type")]
            public string EmploymentType { get; set; } = string.Empty;
            [Name("Contract Duration")]
            public string? ContractDuration { get; set; }
            [Name("Role")]
            public string Role { get; set; } = string.Empty;
            [Name("Rate Type")]
            public string RateType { get; set; } = string.Empty;
            [Name("Rate Amount")]
            public double Rate { get; set; }
            [Name("Branch")]
            public string Branch { get; set; } = string.Empty;
            [Name("Shift Start Time")]
            public string ShiftStartTime { get; set; } = string.Empty;
            [Name("Shift End Time")]
            public string ShiftEndTime { get; set; } = string.Empty;
            [Name("Bank Name")]
            public string? BankName { get; set; }
            [Name("Account Type")]
            public string? AccountType { get; set; }
            [Name("Account Number")]
            public string? AccountNumber { get; set; }
            [Name("Branch Code")]
            public string? BranchCode { get; set; }
            [Name("Annual Leave Balance")]
            public double AnnualLeaveBalance { get; set; }
            [Name("Sick Leave Balance")]
            public double SickLeaveBalance { get; set; }
            [Name("Leave Cycle Start Date")]
            public string? LeaveCycleStartDate { get; set; }
            
            // Legacy/Optional mappings if needed
            public string? DoB { get; set; }
        }
    }
}
