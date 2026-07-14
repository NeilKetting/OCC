using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using OCC.API.Controllers;
using OCC.API.Services;

namespace OCC.Tests
{
    public class DbDebug
    {
        private readonly ITestOutputHelper _output;

        public DbDebug(ITestOutputHelper output)
        {
            _output = output;
        }

        public class ExcelEmpInfo
        {
            public string Bas { get; set; } = "";
            public string Name { get; set; } = "";
            public double NetPay { get; set; }
        }

        [Fact]
        public async Task FindMissingEmployees()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string excelPath = @"c:\Users\Neil\source\repos\OCC\G. JHB 10 JUL 26 (003).xlsx";
            var excelList = new List<ExcelEmpInfo>();

            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables["OCC"] ?? result.Tables[0];

                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    string col1 = row[1]?.ToString()?.Trim() ?? ""; // BAS
                    string col2 = row[2]?.ToString()?.Trim() ?? ""; // NAME

                    if (int.TryParse(col1, out int basNum) && !string.IsNullOrEmpty(col2) && col2 != "NAME")
                    {
                        double net = ParseDouble(row[19]);
                        excelList.Add(new ExcelEmpInfo
                        {
                            Bas = col1.Trim(),
                            Name = col2.Trim(),
                            NetPay = net
                        });
                    }
                }
            }

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(dbOptions);
            var dbEmployees = await context.Employees.ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("# Employee Count & Presence Audit");
            sb.AppendLine();

            sb.AppendLine("## 1. Employees in Excel but MISSING from Database");
            sb.AppendLine("| BAS | Name | Excel Net Pay |");
            sb.AppendLine("|---|---|---|");

            double missingTotal = 0;
            foreach (var excelEmp in excelList.OrderBy(e => e.Name))
            {
                var dbEmp = dbEmployees.FirstOrDefault(e => e.EmployeeNumber?.Trim() == excelEmp.Bas);
                if (dbEmp == null)
                {
                    missingTotal += excelEmp.NetPay;
                    sb.AppendLine($"| {excelEmp.Bas} | {excelEmp.Name} | R {excelEmp.NetPay:F2} |");
                }
            }
            sb.AppendLine($"| | **Total Missing** | **R {missingTotal:F2}** |");
            sb.AppendLine();

            sb.AppendLine("## 2. Employees active in DB JHB but MISSING from Excel");
            sb.AppendLine("| BAS | Name | DB Hourly Rate |");
            sb.AppendLine("|---|---|---|");

            foreach (var dbEmp in dbEmployees.Where(e => e.Status == EmployeeStatus.Active && e.RateType == RateType.Hourly && e.Branch == "Johannesburg").OrderBy(e => e.FirstName))
            {
                var excelEmp = excelList.FirstOrDefault(e => e.Bas == dbEmp.EmployeeNumber?.Trim());
                if (excelEmp == null)
                {
                    sb.AppendLine($"| {dbEmp.EmployeeNumber?.Trim()} | {dbEmp.FirstName} {dbEmp.LastName} | R {dbEmp.HourlyRate:F2} |");
                }
            }

            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\missing_employees.md", sb.ToString());
        }

        private double ParseDouble(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            if (val is double d) return d;
            if (val is float f) return f;
            if (val is int i) return i;
            if (val is decimal dec) return (double)dec;
            
            string str = val.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(str)) return 0;

            if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double res)) return res;
            if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out res)) return res;

            string normalized = str.Replace(",", ".");
            if (double.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out res)) return res;

            string commaSeparated = str.Replace(".", ",");
            if (double.TryParse(commaSeparated, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out res)) return res;

            return 0;
        }

        public class ExcelEmployeeDetails
        {
            public string Bas { get; set; } = "";
            public string Name { get; set; } = "";
            public double Rate { get; set; }
            public double Hours { get; set; }
            public double StdOt { get; set; }
            public double SatOt { get; set; }
            public double SunOt { get; set; }
            public double Loans { get; set; }
            public double Washing { get; set; }
            public double Gas { get; set; }
            public double Other { get; set; }
            public double NetPay { get; set; }
            public string Comments { get; set; } = "";
        }

        [Fact]
        public async Task CompareExcelWithDatabase()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string excelPath = @"c:\Users\Neil\source\repos\OCC\Copy of G. JHB 10 JUL 26 (003).xlsx";
            var excelList = new List<ExcelEmployeeDetails>();

            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables["OCC"] ?? result.Tables[0];

                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    string col1 = row[1]?.ToString()?.Trim() ?? ""; // BAS
                    string col2 = row[2]?.ToString()?.Trim() ?? ""; // NAME

                    if ((int.TryParse(col1, out int basNum) || col1.StartsWith("CAS") || col1 == "0" || col1.StartsWith("4")) && !string.IsNullOrEmpty(col2) && col2 != "NAME")
                    {
                        var emp = new ExcelEmployeeDetails
                        {
                            Bas = col1.Trim(),
                            Name = col2.Trim(),
                            Rate = ParseDouble(row[4]),
                            Hours = ParseDouble(row[5]),
                            StdOt = ParseDouble(row[12]),
                            SatOt = ParseDouble(row[13]),
                            SunOt = ParseDouble(row[14]),
                            Loans = ParseDouble(row[15]),
                            Washing = ParseDouble(row[16]),
                            Gas = ParseDouble(row[17]),
                            Other = ParseDouble(row[18]),
                            NetPay = ParseDouble(row[19]),
                        };

                        if (r + 1 < table.Rows.Count)
                        {
                            var nextRow = table.Rows[r + 1];
                            string nextCol0 = nextRow[0]?.ToString()?.Trim() ?? "";
                            if (nextCol0.Contains("SUPERVISOR FEE"))
                            {
                                emp.Comments = "Supervisor Fee: " + ParseDouble(nextRow[19]).ToString("F2");
                            }
                        }
                        excelList.Add(emp);
                    }
                }
            }

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

             using var context = new AppDbContext(dbOptions);
            
            // Retrieve housed employees count
            var housedCount = await context.Employees.CountAsync(e => e.LivesInCompanyHousing && e.Branch == "Johannesburg" && e.Status == EmployeeStatus.Active && e.RateType == RateType.Hourly);

            // Build the WageCalculationService
            var wageCalc = new WageCalculationService(new WageCalculationOptions());
            var controller = new WageRunsController(context, wageCalc, null!);

            // Generate the draft request
            var draftReq = new WageRun
            {
                StartDate = new DateTime(2026, 6, 27),
                EndDate = new DateTime(2026, 7, 10),
                Branch = "Johannesburg",
                PayType = "Hourly",
                Status = WageRunStatus.Draft,
                InputCompanyHousingWashingFee = 20.00m,
                InputTotalGasCharge = 17.09m * housedCount
            };

            var actionResult = await controller.GenerateDraft(draftReq);
            var okResult = actionResult.Result as OkObjectResult;
            var generatedRun = okResult?.Value as WageRun ?? (actionResult.Value);

            if (generatedRun == null)
            {
                File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\excel_comparison_audit.md", "Error: Failed to generate draft wage run.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Wage Run Comparison: Excel vs Database (JHB Hourly 10 Jul 2026)");
            sb.AppendLine();
            sb.AppendLine("| BAS | Employee Name | Source | Hours | Std OT | Sat OT | Sun OT | Loans | Net Pay | Match Status |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");

            foreach (var excelEmp in excelList.OrderBy(e => e.Name))
            {
                var dbLine = generatedRun.Lines.FirstOrDefault(l => 
                    l.EmployeeNumber?.Trim() == excelEmp.Bas ||
                    (excelEmp.Bas == "0" && (
                        (excelEmp.Name == "TIMOTHY MASETE" && l.EmployeeName.Contains("MASETE") && l.EmployeeName.Contains("TIMOTHY")) ||
                        (excelEmp.Name != "TIMOTHY MASETE" && (
                            l.EmployeeName.Contains(excelEmp.Name, StringComparison.OrdinalIgnoreCase) ||
                            excelEmp.Name.Contains(l.EmployeeName.Split(' ')[0], StringComparison.OrdinalIgnoreCase) ||
                            (excelEmp.Name == "DAVID RAPHETYE" && l.EmployeeName.Contains("RATHEPYE")) ||
                            (excelEmp.Name == "SIPHO KHUMALO" && l.EmployeeName.Contains("KHUMALO"))
                        ))
                    ))
                );
                
                if (dbLine == null)
                {
                    sb.AppendLine($"| {excelEmp.Bas} | {excelEmp.Name} | **Excel** | {excelEmp.Hours:F2} | {excelEmp.StdOt:F2} | {excelEmp.SatOt:F2} | {excelEmp.SunOt:F2} | {excelEmp.Loans:F2} | R {excelEmp.NetPay:F2} | **MISSING IN DB** |");
                    continue;
                }

                // Check differences
                bool hoursMatch = Math.Abs(excelEmp.Hours - (dbLine.NormalHours + dbLine.ProjectedHours + dbLine.VarianceHours)) < 0.05;
                bool stdOtMatch = Math.Abs(excelEmp.StdOt - dbLine.Overtime15Hours) < 0.05;
                bool satOtMatch = Math.Abs(excelEmp.SatOt - dbLine.SaturdayOvertimeHours) < 0.05;
                bool sunOtMatch = Math.Abs(excelEmp.SunOt - dbLine.Overtime20Hours) < 0.05;
                bool loansMatch = Math.Abs(excelEmp.Loans - (double)dbLine.DeductionLoan) < 0.05;
                bool netMatch = Math.Abs(excelEmp.NetPay - (double)dbLine.NetPay) < 0.50; // allow small rounding diffs

                string matchStatus = (hoursMatch && stdOtMatch && satOtMatch && sunOtMatch && loansMatch && netMatch) ? "✅ Match" : "❌ MISMATCH";

                sb.AppendLine($"| {excelEmp.Bas} | {excelEmp.Name} | **Excel** | {excelEmp.Hours:F2} | {excelEmp.StdOt:F2} | {excelEmp.SatOt:F2} | {excelEmp.SunOt:F2} | {excelEmp.Loans:F2} | R {excelEmp.NetPay:F2} | {matchStatus} |");
                sb.AppendLine($"| | | **DB** | {(dbLine.NormalHours + dbLine.ProjectedHours + dbLine.VarianceHours):F2} | {dbLine.Overtime15Hours:F2} | {dbLine.SaturdayOvertimeHours:F2} | {dbLine.Overtime20Hours:F2} | {dbLine.DeductionLoan:F2} | R {dbLine.NetPay:F2} | |");
            }

            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\excel_comparison_audit.md", sb.ToString());
        }

        [Fact]
        public void VerifySafeWorkingHours()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;
            using var context = new AppDbContext(dbOptions);

            var project = context.Projects.FirstOrDefault(p => p.Name.Contains("Engen Mossel Bay POW 1.2"));
            if (project == null)
            {
                File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\safe_hours_verification.md", "Project not found in database.");
                return;
            }

            var records = context.AttendanceRecords
                .Where(r => r.ProjectId == project.Id && r.Status == AttendanceStatus.Present)
                .OrderBy(r => r.Date)
                .ToList();

            var totalHours = records.Sum(r => r.HoursWorked);

            var sb = new StringBuilder();
            sb.AppendLine($"# Safe Working Hours Verification for project: {project.Name}");
            sb.AppendLine($"* **Project ID:** {project.Id}");
            sb.AppendLine($"* **Total Safe Working Hours calculated in DB:** {totalHours:N2} Hours");
            sb.AppendLine();
            sb.AppendLine("## Attendance Records contributing to the total:");
            sb.AppendLine("| Date | Employee ID | Hours Worked | Status |");
            sb.AppendLine("|---|---|---|---|");

            foreach (var r in records)
            {
                sb.AppendLine($"| {r.Date:yyyy-MM-dd} | {r.EmployeeId} | {r.HoursWorked:F2} | {r.Status} |");
            }

            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\safe_hours_verification.md", sb.ToString());
        }

        [Fact]
        public async Task CheckCasualRates()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(dbOptions);
            var casuals = await context.Employees
                .Where(e => e.EmployeeNumber.StartsWith("CAS") || e.EmployeeNumber == "489")
                .ToListAsync();

            var sb = new StringBuilder();
            foreach (var c in casuals)
            {
                sb.AppendLine($"BAS: {c.EmployeeNumber}, Name: {c.FirstName} {c.LastName}, Rate: R {c.HourlyRate:F2}");
            }
            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\casual_rates_check.md", sb.ToString());
        }

        [Fact]
        public async Task CompareBankingDetails()
        {
            var oldConnStr = "Server=localhost\\SQLEXPRESS01;Database=OCC_Rev5_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
            var newConnStr = "Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

            var oldBanking = new List<(string Bas, string Name, string Bank, string Account, string BranchCode, string AccountType)>();
            var newBanking = new List<(string Bas, string Name, string Bank, string Account, string BranchCode, string AccountType)>();

            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(oldConnStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT EmployeeNumber, FirstName, LastName, BankName, AccountNumber, BranchCode, AccountType FROM Employees";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            oldBanking.Add((
                                reader["EmployeeNumber"]?.ToString()?.Trim() ?? "",
                                $"{reader["FirstName"]} {reader["LastName"]}".Trim(),
                                reader["BankName"]?.ToString()?.Trim() ?? "",
                                reader["AccountNumber"]?.ToString()?.Trim() ?? "",
                                reader["BranchCode"]?.ToString()?.Trim() ?? "",
                                reader["AccountType"]?.ToString()?.Trim() ?? ""
                            ));
                        }
                    }
                }
            }

            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(newConnStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT EmployeeNumber, FirstName, LastName, BankName, AccountNumber, BranchCode, AccountType FROM Employees";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            newBanking.Add((
                                reader["EmployeeNumber"]?.ToString()?.Trim() ?? "",
                                $"{reader["FirstName"]} {reader["LastName"]}".Trim(),
                                reader["BankName"]?.ToString()?.Trim() ?? "",
                                reader["AccountNumber"]?.ToString()?.Trim() ?? "",
                                reader["BranchCode"]?.ToString()?.Trim() ?? "",
                                reader["AccountType"]?.ToString()?.Trim() ?? ""
                            ));
                        }
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Banking Details Comparison: Old vs New Database");
            sb.AppendLine();
            sb.AppendLine("This report compares the banking details stored in the old `OCC_Rev5_DB` database vs the current `OCC_V2_DB` database.");
            sb.AppendLine();
            sb.AppendLine("| BAS | Name | Old Bank | Old Account | New Bank | New Account | Status |");
            sb.AppendLine("|---|---|---|---|---|---|---|");

            int missingCount = 0;
            var sqlUpdates = new StringBuilder();
            sqlUpdates.AppendLine("BEGIN TRANSACTION;");

            foreach (var oldB in oldBanking.OrderBy(o => o.Name))
            {
                if (string.IsNullOrEmpty(oldB.Bas)) continue;

                var newB = newBanking.FirstOrDefault(n => n.Bas == oldB.Bas);
                if (newB.Name == null)
                {
                    sb.AppendLine($"| {oldB.Bas} | {oldB.Name} | {oldB.Bank} | {oldB.Account} | N/A (Not in V2) | N/A | Missing from V2 |");
                    continue;
                }

                bool oldHasDetails = !string.IsNullOrEmpty(oldB.Bank) || !string.IsNullOrEmpty(oldB.Account);
                bool newHasDetails = !string.IsNullOrEmpty(newB.Bank) || !string.IsNullOrEmpty(newB.Account);

                if (oldHasDetails && !newHasDetails)
                {
                    sb.AppendLine($"| {oldB.Bas} | {oldB.Name} | {oldB.Bank} | {oldB.Account} | *Empty* | *Empty* | **Missing in V2 (To Restore)** |");
                    
                    string bankEnumVal = MapToV2BankName(oldB.Bank);
                    string accEsc = oldB.Account?.Replace("'", "''") ?? "";
                    string branchEsc = oldB.BranchCode?.Replace("'", "''") ?? "";
                    
                    // Normalize account type (Savings, Cheque, Transmission)
                    string typeEsc = oldB.AccountType?.Replace("'", "''") ?? "Savings";
                    if (typeEsc.ToLower().Contains("save") || typeEsc.ToLower().Contains("savings")) typeEsc = "Savings";
                    else if (typeEsc.ToLower().Contains("cheq") || typeEsc.ToLower().Contains("current")) typeEsc = "Cheque";
                    else if (typeEsc.ToLower().Contains("trans")) typeEsc = "Transmission";

                    sqlUpdates.AppendLine($"UPDATE Employees SET BankName = {(bankEnumVal == "NULL" ? "NULL" : $"'{bankEnumVal}'")}, AccountNumber = {(string.IsNullOrEmpty(accEsc) ? "NULL" : $"'{accEsc}'")}, BranchCode = {(string.IsNullOrEmpty(branchEsc) ? "NULL" : $"'{branchEsc}'")}, AccountType = '{(string.IsNullOrEmpty(typeEsc) ? "Savings" : typeEsc)}' WHERE EmployeeNumber = '{oldB.Bas}'; -- {oldB.Name}");
                    
                    missingCount++;
                }
                else if (oldHasDetails && newHasDetails && (oldB.Bank != newB.Bank || oldB.Account != newB.Account))
                {
                    sb.AppendLine($"| {oldB.Bas} | {oldB.Name} | {oldB.Bank} | {oldB.Account} | {newB.Bank} | {newB.Account} | **Mismatched** |");
                }
                else
                {
                    sb.AppendLine($"| {oldB.Bas} | {oldB.Name} | {oldB.Bank} | {oldB.Account} | {newB.Bank} | {newB.Account} | Match |");
                }
            }

            sqlUpdates.AppendLine("COMMIT TRANSACTION;");

            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\banking_comparison_report.md", sb.ToString());
            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\restore_banking_details.sql", sqlUpdates.ToString());
        }

        private string MapToV2BankName(string oldBank)
        {
            if (string.IsNullOrEmpty(oldBank)) return "NULL";
            
            string normalized = oldBank.Replace(" ", "").Replace("/", "").Replace("_", "").Replace("-", "").ToLower();
            
            if (normalized.Contains("capitecbusiness")) return "CapitecBusiness";
            if (normalized.Contains("capitec")) return "Capitec";
            if (normalized.Contains("nedbank")) return "Nedbank";
            if (normalized.Contains("fnb") || normalized.Contains("rmb")) return "FNB_RMB";
            if (normalized.Contains("standard")) return "StandardBank";
            if (normalized.Contains("tyme")) return "TymeBank";
            if (normalized.Contains("bidvest")) return "BidvestBank";
            if (normalized.Contains("access")) return "AccessBank";
            if (normalized.Contains("absa")) return "ABSA";
            if (normalized.Contains("africanbank")) return "AfricanBank";
            if (normalized.Contains("discovery")) return "DiscoveryBank";
            
            // Fallback to Enum value name
            foreach (BankName bank in Enum.GetValues(typeof(BankName)))
            {
                if (bank == BankName.None) continue;
                if (bank.ToString().ToLower() == normalized)
                    return bank.ToString();
            }
            
            return oldBank; // Fallback to raw if not matched
        }

        [Fact]
        public async Task UpdateEmployeeRatesFromExcel()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string excelPath = @"c:\Users\Neil\source\repos\OCC\Copy of G. JHB 10 JUL 26 (003).xlsx";
            var excelList = new List<ExcelEmployeeDetails>();

            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables["OCC"] ?? result.Tables[0];

                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    string col1 = row[1]?.ToString()?.Trim() ?? ""; // BAS
                    string col2 = row[2]?.ToString()?.Trim() ?? ""; // NAME

                    if ((int.TryParse(col1, out int basNum) || col1 == "0" || col1.StartsWith("CAS")) && !string.IsNullOrEmpty(col2) && col2 != "NAME")
                    {
                        var emp = new ExcelEmployeeDetails
                        {
                            Bas = col1.Trim(),
                            Name = col2.Trim(),
                            Rate = ParseDouble(row[4]),
                        };
                        excelList.Add(emp);
                    }
                }
            }

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(dbOptions);
            var dbEmployees = await context.Employees.ToListAsync();

            int updatedCount = 0;
            var sb = new StringBuilder();
            sb.AppendLine("# Employee Hourly Rates Update Report");
            sb.AppendLine();
            sb.AppendLine("| BAS | Employee Name | Old Rate | New Rate | Status |");
            sb.AppendLine("|---|---|---|---|---|");

            foreach (var excelEmp in excelList)
            {
                var dbEmp = dbEmployees.FirstOrDefault(e => e.EmployeeNumber?.Trim() == excelEmp.Bas);
                if (dbEmp != null)
                {
                    double oldRate = dbEmp.HourlyRate;
                    double newRate = excelEmp.Rate;

                    if (Math.Abs(oldRate - newRate) > 0.001)
                    {
                        dbEmp.HourlyRate = newRate;
                        sb.AppendLine($"| {excelEmp.Bas} | {dbEmp.FirstName} {dbEmp.LastName} | R {oldRate:F2} | R {newRate:F2} | Updated |");
                        updatedCount++;
                    }
                    else
                    {
                        sb.AppendLine($"| {excelEmp.Bas} | {dbEmp.FirstName} {dbEmp.LastName} | R {oldRate:F2} | R {newRate:F2} | Match (No Change) |");
                    }
                }
                else
                {
                    sb.AppendLine($"| {excelEmp.Bas} | {excelEmp.Name} | N/A | R {excelEmp.Rate:F2} | WARNING: Not in DB |");
                }
            }

            if (updatedCount > 0)
            {
                await context.SaveChangesAsync();
                sb.AppendLine();
                sb.AppendLine($"**Successfully updated {updatedCount} employee rates in the database.**");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("**No rates were updated (all database rates already match the Excel file).**");
            }

            string reportPath = @"c:\Users\Neil\source\repos\OCC\rates_update_report.md";
            File.WriteAllText(reportPath, sb.ToString());
            _output.WriteLine(sb.ToString());
        }

        [Fact]
        public void DumpPdfText()
        {
            string pdfPath = @"C:\Users\Neil\Documents\OCC\WageRuns\WageRun_Johannesburg_20260710_082216.pdf";
            var sb = new StringBuilder();
            
            using (var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath))
            {
                for (int i = 1; i <= document.NumberOfPages; i++)
                {
                    var page = document.GetPage(i);
                    sb.AppendLine($"--- PAGE {i} ---");
                    sb.AppendLine(page.Text);
                }
            }

            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\pdf_text_dump.txt", sb.ToString());
        }

        [Fact]
        public async Task PortSuppliers()
        {
            string oldConnStr = "Server=localhost\\SQLEXPRESS01;Database=OCC_Rev5_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
            string newConnStr = "Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

            using var oldConn = new Microsoft.Data.SqlClient.SqlConnection(oldConnStr);
            using var newConn = new Microsoft.Data.SqlClient.SqlConnection(newConnStr);

            await oldConn.OpenAsync();
            await newConn.OpenAsync();

            // 1. Get old schema columns
            var oldCols = new List<string>();
            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Suppliers'", oldConn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    oldCols.Add(reader.GetString(0));
                }
            }

            // 2. Get new schema columns
            var newCols = new List<string>();
            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Suppliers'", newConn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    newCols.Add(reader.GetString(0));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Suppliers Migration Schema Comparison");
            sb.AppendLine();
            sb.AppendLine("## Old Schema Columns");
            sb.AppendLine(string.Join(", ", oldCols));
            sb.AppendLine();
            sb.AppendLine("## New Schema Columns");
            sb.AppendLine(string.Join(", ", newCols));
            sb.AppendLine();

            // Find missing in new vs old
            var missingInNew = oldCols.Except(newCols).ToList();
            var addedInNew = newCols.Except(oldCols).ToList();
            sb.AppendLine($"### Columns missing in V2: {string.Join(", ", missingInNew)}");
            sb.AppendLine($"### Columns added in V2: {string.Join(", ", addedInNew)}");
            sb.AppendLine();

            // Get existing suppliers in new DB to avoid duplicates
            var existingNewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT Name FROM Suppliers", newConn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingNewNames.Add(reader.GetString(0).Trim());
                }
            }

            // Read old suppliers
            var oldSuppliers = new List<Dictionary<string, object>>();
            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT * FROM Suppliers", oldConn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var dict = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        dict[reader.GetName(i)] = reader.GetValue(i);
                    }
                    oldSuppliers.Add(dict);
                }
            }

            // Perform migration locally and generate SQL sync script
            var sqlStatements = new List<string>();
            int importedCount = 0;
            int skippedCount = 0;

            sb.AppendLine("## Migration Execution Summary");
            sb.AppendLine("| Status | Name | City | Contact | Phone |");
            sb.AppendLine("|---|---|---|---|---|");

            foreach (var oldSup in oldSuppliers)
            {
                string name = oldSup.GetValueOrDefault("Name")?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(name)) continue;

                if (existingNewNames.Contains(name))
                {
                    skippedCount++;
                    sb.AppendLine($"| SKIPPED (Exists) | {name} | | | |");
                    continue;
                }

                // Gather columns
                Guid id = Guid.NewGuid();
                // Map fields checking if they exist in old schema, else use default
                string address = oldSup.GetValueOrDefault("Address")?.ToString() ?? "";
                string city = oldSup.GetValueOrDefault("City")?.ToString() ?? "";
                string postalCode = oldSup.GetValueOrDefault("PostalCode")?.ToString() ?? "";
                string phone = oldSup.GetValueOrDefault("Phone")?.ToString() ?? "";
                string contactPerson = oldSup.GetValueOrDefault("ContactPerson")?.ToString() ?? "";
                string email = oldSup.GetValueOrDefault("Email")?.ToString() ?? "";
                string vatNumber = oldSup.GetValueOrDefault("VatNumber")?.ToString() ?? "";
                string bankName = oldSup.GetValueOrDefault("BankName")?.ToString() ?? "";
                string bankAccountNumber = oldSup.GetValueOrDefault("BankAccountNumber")?.ToString() ?? "";
                string branchCode = oldSup.GetValueOrDefault("BranchCode")?.ToString() ?? "";
                string supplierAccountNumber = oldSup.GetValueOrDefault("SupplierAccountNumber")?.ToString() ?? "";
                
                object branchVal = DBNull.Value;
                if (oldSup.ContainsKey("Branch") && oldSup["Branch"] != DBNull.Value)
                {
                    branchVal = oldSup["Branch"];
                }

                // V2 auditable columns
                DateTime createdAt = DateTime.UtcNow;
                string createdBy = "System_Migration";
                bool isActive = true;

                // Let's perform INSERT locally into OCC_V2_DB
                using (var insertCmd = new Microsoft.Data.SqlClient.SqlCommand(
                    @"INSERT INTO Suppliers (Id, Name, Address, City, PostalCode, Phone, ContactPerson, Email, VatNumber, BankName, BankAccountNumber, BranchCode, SupplierAccountNumber, Branch, CreatedAtUtc, CreatedBy, IsActive)
                      VALUES (@Id, @Name, @Address, @City, @PostalCode, @Phone, @ContactPerson, @Email, @VatNumber, @BankName, @BankAccountNumber, @BranchCode, @SupplierAccountNumber, @Branch, @CreatedAtUtc, @CreatedBy, @IsActive)", newConn))
                {
                    insertCmd.Parameters.AddWithValue("@Id", id);
                    insertCmd.Parameters.AddWithValue("@Name", name);
                    insertCmd.Parameters.AddWithValue("@Address", address);
                    insertCmd.Parameters.AddWithValue("@City", city);
                    insertCmd.Parameters.AddWithValue("@PostalCode", postalCode);
                    insertCmd.Parameters.AddWithValue("@Phone", phone);
                    insertCmd.Parameters.AddWithValue("@ContactPerson", contactPerson);
                    insertCmd.Parameters.AddWithValue("@Email", email);
                    insertCmd.Parameters.AddWithValue("@VatNumber", vatNumber);
                    insertCmd.Parameters.AddWithValue("@BankName", bankName);
                    insertCmd.Parameters.AddWithValue("@BankAccountNumber", bankAccountNumber);
                    insertCmd.Parameters.AddWithValue("@BranchCode", branchCode);
                    insertCmd.Parameters.AddWithValue("@SupplierAccountNumber", supplierAccountNumber);
                    insertCmd.Parameters.AddWithValue("@Branch", branchVal);
                    insertCmd.Parameters.AddWithValue("@CreatedAtUtc", createdAt);
                    insertCmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                    insertCmd.Parameters.AddWithValue("@IsActive", isActive);

                    await insertCmd.ExecuteNonQueryAsync();
                }

                // Add to SQL script list
                string sqlBranch = branchVal == DBNull.Value ? "NULL" : (branchVal.ToString() ?? "NULL");
                string sqlInsert = $"INSERT INTO Suppliers (Id, Name, Address, City, PostalCode, Phone, ContactPerson, Email, VatNumber, BankName, BankAccountNumber, BranchCode, SupplierAccountNumber, Branch, CreatedAtUtc, CreatedBy, IsActive) " +
                                   $"VALUES ('{id}', {EscapeSql(name)}, {EscapeSql(address)}, {EscapeSql(city)}, {EscapeSql(postalCode)}, {EscapeSql(phone)}, {EscapeSql(contactPerson)}, {EscapeSql(email)}, {EscapeSql(vatNumber)}, {EscapeSql(bankName)}, {EscapeSql(bankAccountNumber)}, {EscapeSql(branchCode)}, {EscapeSql(supplierAccountNumber)}, {sqlBranch}, GETUTCDATE(), 'System_Migration', 1);";
                sqlStatements.Add(sqlInsert);

                importedCount++;
                sb.AppendLine($"| IMPORTED | {name} | {city} | {contactPerson} | {phone} |");
            }

            sb.AppendLine();
            sb.AppendLine($"**Summary:** Imported {importedCount} new suppliers, skipped {skippedCount} existing suppliers.");

            // Write report
            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\suppliers_migration_report.md", sb.ToString());

            // Write SQL sync script
            var sqlScript = new StringBuilder();
            sqlScript.AppendLine("-- Suppliers Sync Script (Old DB -> V2 DB)");
            sqlScript.AppendLine("USE OCC_V2_DB;");
            sqlScript.AppendLine("GO");
            sqlScript.AppendLine();
            sqlScript.AppendLine("BEGIN TRANSACTION;");
            sqlScript.AppendLine("BEGIN TRY");
            sqlScript.AppendLine();
            foreach (var stmt in sqlStatements)
            {
                sqlScript.AppendLine(stmt);
            }
            sqlScript.AppendLine();
            sqlScript.AppendLine("    COMMIT TRANSACTION;");
            sqlScript.AppendLine("    PRINT 'Suppliers migrated successfully!';");
            sqlScript.AppendLine("END TRY");
            sqlScript.AppendLine("BEGIN CATCH");
            sqlScript.AppendLine("    ROLLBACK TRANSACTION;");
            sqlScript.AppendLine("    PRINT 'Error occurred during migration:';");
            sqlScript.AppendLine("    PRINT ERROR_MESSAGE();");
            sqlScript.AppendLine("END CATCH");
            sqlScript.AppendLine("GO");

            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\migrate_suppliers.sql", sqlScript.ToString());
            _output.WriteLine(sb.ToString());
        }

        [Fact]
        public async Task PortInventoryItems()
        {
            string oldConnStr = "Server=localhost\\SQLEXPRESS01;Database=OCC_Rev5_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
            string newConnStr = "Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

            using var oldConn = new Microsoft.Data.SqlClient.SqlConnection(oldConnStr);
            using var newConn = new Microsoft.Data.SqlClient.SqlConnection(newConnStr);

            await oldConn.OpenAsync();
            await newConn.OpenAsync();

            // 1. Get old schema columns
            var oldCols = new List<string>();
            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InventoryItems'", oldConn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    oldCols.Add(reader.GetString(0));
                }
            }

            // 2. Get new schema columns
            var newCols = new List<string>();
            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InventoryItems'", newConn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    newCols.Add(reader.GetString(0));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Inventory Items Migration Schema Comparison");
            sb.AppendLine();
            sb.AppendLine("## Old Schema Columns");
            sb.AppendLine(string.Join(", ", oldCols));
            sb.AppendLine();
            sb.AppendLine("## New Schema Columns");
            sb.AppendLine(string.Join(", ", newCols));
            sb.AppendLine();

            var missingInNew = oldCols.Except(newCols).ToList();
            var addedInNew = newCols.Except(oldCols).ToList();
            sb.AppendLine($"### Columns missing in V2: {string.Join(", ", missingInNew)}");
            sb.AppendLine($"### Columns added in V2: {string.Join(", ", addedInNew)}");
            sb.AppendLine();

            // 3. Get existing inventory items in new DB to avoid duplicates
            var existingSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existingDescriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT Sku, Description FROM InventoryItems", newConn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    string sku = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim();
                    string desc = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim();
                    if (!string.IsNullOrEmpty(sku)) existingSkus.Add(sku);
                    if (!string.IsNullOrEmpty(desc)) existingDescriptions.Add(desc);
                }
            }

            // 4. Read old inventory items
            var oldItems = new List<Dictionary<string, object>>();
            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT * FROM InventoryItems", oldConn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var dict = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        dict[reader.GetName(i)] = reader.GetValue(i);
                    }
                    oldItems.Add(dict);
                }
            }

            var sqlStatements = new List<string>();
            int importedCount = 0;
            int skippedCount = 0;

            sb.AppendLine("## Migration Execution Summary");
            sb.AppendLine("| Status | SKU | Description | Supplier | Category | Price |");
            sb.AppendLine("|---|---|---|---|---|---|");

            foreach (var oldItem in oldItems)
            {
                string sku = oldItem.GetValueOrDefault("Sku")?.ToString()?.Trim() ?? "";
                string desc = oldItem.GetValueOrDefault("Description")?.ToString()?.Trim() ?? "";
                
                if (string.IsNullOrEmpty(desc) && oldItem.ContainsKey("ProductName"))
                {
                    desc = oldItem["ProductName"]?.ToString()?.Trim() ?? "";
                }

                if (string.IsNullOrEmpty(desc)) continue;

                bool isDuplicate = false;
                if (!string.IsNullOrEmpty(sku) && existingSkus.Contains(sku)) isDuplicate = true;
                if (existingDescriptions.Contains(desc)) isDuplicate = true;

                if (isDuplicate)
                {
                    skippedCount++;
                    sb.AppendLine($"| SKIPPED (Exists) | {sku} | {desc} | | | |");
                    continue;
                }

                Guid id = Guid.NewGuid();
                string supplier = oldItem.GetValueOrDefault("Supplier")?.ToString() ?? "";
                string category = oldItem.GetValueOrDefault("Category")?.ToString() ?? "General";
                string location = oldItem.GetValueOrDefault("Location")?.ToString() ?? "Warehouse";
                double jhbQty = Convert.ToDouble(oldItem.GetValueOrDefault("JhbQuantity") ?? 0.0);
                double cptQty = Convert.ToDouble(oldItem.GetValueOrDefault("CptQuantity") ?? 0.0);
                double jhbReorder = Convert.ToDouble(oldItem.GetValueOrDefault("JhbReorderPoint") ?? 0.0);
                double cptReorder = Convert.ToDouble(oldItem.GetValueOrDefault("CptReorderPoint") ?? 0.0);
                string uom = oldItem.GetValueOrDefault("UnitOfMeasure")?.ToString() ?? "ea";
                decimal avgCost = Convert.ToDecimal(oldItem.GetValueOrDefault("AverageCost") ?? 0m);
                decimal price = Convert.ToDecimal(oldItem.GetValueOrDefault("Price") ?? 0m);
                bool trackLow = Convert.ToBoolean(oldItem.GetValueOrDefault("TrackLowStock") ?? true);
                int type = Convert.ToInt32(oldItem.GetValueOrDefault("Type") ?? 1);

                DateTime createdAt = DateTime.UtcNow;
                string createdBy = "System_Migration";
                bool isActive = true;

                // Perform local insert
                using (var insertCmd = new Microsoft.Data.SqlClient.SqlCommand(
                    @"INSERT INTO InventoryItems (Id, Description, Supplier, Category, Location, JhbQuantity, CptQuantity, JhbReorderPoint, CptReorderPoint, UnitOfMeasure, Sku, AverageCost, Price, TrackLowStock, Type, QuantityOnHand, CreatedAtUtc, CreatedBy, IsActive)
                      VALUES (@Id, @Description, @Supplier, @Category, @Location, @JhbQuantity, @CptQuantity, @JhbReorderPoint, @CptReorderPoint, @UnitOfMeasure, @Sku, @AverageCost, @Price, @TrackLowStock, @Type, @QuantityOnHand, @CreatedAtUtc, @CreatedBy, @IsActive)", newConn))
                {
                    insertCmd.Parameters.AddWithValue("@Id", id);
                    insertCmd.Parameters.AddWithValue("@Description", desc);
                    insertCmd.Parameters.AddWithValue("@Supplier", supplier);
                    insertCmd.Parameters.AddWithValue("@Category", category);
                    insertCmd.Parameters.AddWithValue("@Location", location);
                    insertCmd.Parameters.AddWithValue("@JhbQuantity", jhbQty);
                    insertCmd.Parameters.AddWithValue("@CptQuantity", cptQty);
                    insertCmd.Parameters.AddWithValue("@JhbReorderPoint", jhbReorder);
                    insertCmd.Parameters.AddWithValue("@CptReorderPoint", cptReorder);
                    insertCmd.Parameters.AddWithValue("@UnitOfMeasure", uom);
                    insertCmd.Parameters.AddWithValue("@Sku", sku);
                    insertCmd.Parameters.AddWithValue("@AverageCost", avgCost);
                    insertCmd.Parameters.AddWithValue("@Price", price);
                    insertCmd.Parameters.AddWithValue("@TrackLowStock", trackLow);
                    insertCmd.Parameters.AddWithValue("@Type", type);
                    insertCmd.Parameters.AddWithValue("@QuantityOnHand", jhbQty + cptQty);
                    insertCmd.Parameters.AddWithValue("@CreatedAtUtc", createdAt);
                    insertCmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                    insertCmd.Parameters.AddWithValue("@IsActive", isActive);

                    await insertCmd.ExecuteNonQueryAsync();
                }

                string sqlInsert = $"INSERT INTO InventoryItems (Id, Description, Supplier, Category, Location, JhbQuantity, CptQuantity, JhbReorderPoint, CptReorderPoint, UnitOfMeasure, Sku, AverageCost, Price, TrackLowStock, Type, QuantityOnHand, CreatedAtUtc, CreatedBy, IsActive) " +
                                   $"VALUES ('{id}', {EscapeSql(desc)}, {EscapeSql(supplier)}, {EscapeSql(category)}, {EscapeSql(location)}, " +
                                   $"{jhbQty.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                                   $"{cptQty.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                                   $"{jhbReorder.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                                   $"{cptReorder.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                                   $"{EscapeSql(uom)}, {EscapeSql(sku)}, " +
                                   $"{avgCost.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                                   $"{price.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                                   $"{(trackLow ? 1 : 0)}, {type}, " +
                                   $"{(jhbQty + cptQty).ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                                   $"GETUTCDATE(), 'System_Migration', 1);";
                sqlStatements.Add(sqlInsert);

                importedCount++;
                sb.AppendLine($"| IMPORTED | {sku} | {desc} | {supplier} | {category} | R {price:F2} |");
            }

            sb.AppendLine();
            sb.AppendLine($"**Summary:** Imported {importedCount} new inventory items, skipped {skippedCount} existing items.");

            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\inventory_migration_report.md", sb.ToString());

            var sqlScript = new StringBuilder();
            sqlScript.AppendLine("-- Inventory Items Sync Script (Old DB -> V2 DB)");
            sqlScript.AppendLine("USE OCC_V2_DB;");
            sqlScript.AppendLine("GO");
            sqlScript.AppendLine();
            sqlScript.AppendLine("BEGIN TRANSACTION;");
            sqlScript.AppendLine("BEGIN TRY");
            sqlScript.AppendLine();
            foreach (var stmt in sqlStatements)
            {
                sqlScript.AppendLine(stmt);
            }
            sqlScript.AppendLine();
            sqlScript.AppendLine("    COMMIT TRANSACTION;");
            sqlScript.AppendLine("    PRINT 'Inventory items migrated successfully!';");
            sqlScript.AppendLine("END TRY");
            sqlScript.AppendLine("BEGIN CATCH");
            sqlScript.AppendLine("    ROLLBACK TRANSACTION;");
            sqlScript.AppendLine("    PRINT 'Error occurred during migration:';");
            sqlScript.AppendLine("    PRINT ERROR_MESSAGE();");
            sqlScript.AppendLine("END CATCH");
            sqlScript.AppendLine("GO");

            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\migrate_inventory.sql", sqlScript.ToString());
            _output.WriteLine(sb.ToString());
        }

        [Fact]
        public async Task DumpEmployeeAttendance()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(dbOptions);
            var heris = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeNumber == "460");
            var herman = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeNumber == "399");

            var sb = new StringBuilder();
            sb.AppendLine("# Daily Attendance Dump for Heris (460) and Herman (399)");
            sb.AppendLine();

            if (heris != null)
            {
                sb.AppendLine("## Heris Mthombeni (460)");
                sb.AppendLine("| Date | Status | Check In | Check Out | Hours |");
                sb.AppendLine("|---|---|---|---|---|");
                var records = await context.AttendanceRecords
                    .Where(r => r.EmployeeId == heris.Id && r.Date >= new DateTime(2026, 6, 27) && r.Date <= new DateTime(2026, 7, 10))
                    .OrderBy(r => r.Date)
                    .ToListAsync();
                foreach (var r in records)
                {
                    sb.AppendLine($"| {r.Date:yyyy/MM/dd} | {r.Status} | {r.CheckInTime:HH:mm} | {r.CheckOutTime:HH:mm} | {r.HoursWorked:F2} |");
                }
                sb.AppendLine();
            }

            if (herman != null)
            {
                sb.AppendLine("## Herman Ngidi (399)");
                sb.AppendLine("| Date | Status | Check In | Check Out | Hours |");
                sb.AppendLine("|---|---|---|---|---|");
                var records = await context.AttendanceRecords
                    .Where(r => r.EmployeeId == herman.Id && r.Date >= new DateTime(2026, 6, 27) && r.Date <= new DateTime(2026, 7, 10))
                    .OrderBy(r => r.Date)
                    .ToListAsync();
                foreach (var r in records)
                {
                    sb.AppendLine($"| {r.Date:yyyy/MM/dd} | {r.Status} | {r.CheckInTime:HH:mm} | {r.CheckOutTime:HH:mm} | {r.HoursWorked:F2} |");
                }
                sb.AppendLine();
            }

            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\employee_attendance_dump.md", sb.ToString());
        }

        [Fact]
        public async Task PrintEmployeeShiftDetails()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(dbOptions);
            var heris = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeNumber == "460");
            var herman = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeNumber == "399");

            var sb = new StringBuilder();
            if (heris != null)
            {
                sb.AppendLine($"Heris (460): Start={heris.ShiftStartTime}, End={heris.ShiftEndTime}, LivesInHousing={heris.LivesInCompanyHousing}");
            }
            if (herman != null)
            {
                sb.AppendLine($"Herman (399): Start={herman.ShiftStartTime}, End={herman.ShiftEndTime}, LivesInHousing={herman.LivesInCompanyHousing}");
            }
            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\employee_shifts.txt", sb.ToString());
        }

        [Fact]
        public async Task DumpCompanyProfile()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(dbOptions);
            var profile = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "CompanyProfile");
            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\company_profile.json", profile?.Value ?? "{}");
        }

        [Fact]
        public async Task DumpMismatchedSaturdays()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(dbOptions);
            var empNumbers = new[] { "331", "462", "453", "116", "341" };
            
            var sb = new StringBuilder();
            sb.AppendLine("# Saturday Mismatch Daily Dump");
            sb.AppendLine();

            foreach (var num in empNumbers)
            {
                var emp = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeNumber == num);
                if (emp != null)
                {
                    sb.AppendLine($"## {emp.FirstName} {emp.LastName} ({num})");
                    sb.AppendLine("| Date | Status | Check In | Check Out | Hours |");
                    sb.AppendLine("|---|---|---|---|---|");
                    var dbRecords = await context.AttendanceRecords
                        .Where(r => r.EmployeeId == emp.Id && r.Date >= new DateTime(2026, 6, 27) && r.Date <= new DateTime(2026, 7, 10))
                        .OrderBy(r => r.Date)
                        .ToListAsync();
                    var records = dbRecords.Where(r => r.Date.DayOfWeek == DayOfWeek.Saturday).ToList();
                    foreach (var r in records)
                    {
                        sb.AppendLine($"| {r.Date:yyyy/MM/dd} | {r.Status} | {r.CheckInTime:HH:mm} | {r.CheckOutTime:HH:mm} | {r.HoursWorked:F2} |");
                    }
                    sb.AppendLine();
                }
            }
            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\saturday_mismatches_dump.md", sb.ToString());
        }

        [Fact]
        public async Task DumpMismatchDetails()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(dbOptions);
            var empNumbers = new[] { "471", "423", "340", "464" };
            
            var sb = new StringBuilder();
            sb.AppendLine("# Mismatch Details Daily Dump");
            sb.AppendLine();

            foreach (var num in empNumbers)
            {
                var emp = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeNumber == num);
                if (emp != null)
                {
                    sb.AppendLine($"## {emp.FirstName} {emp.LastName} ({num})");
                    sb.AppendLine("| Date | Status | Check In | Check Out | Hours |");
                    sb.AppendLine("|---|---|---|---|---|");
                    var records = await context.AttendanceRecords
                        .Where(r => r.EmployeeId == emp.Id && r.Date >= new DateTime(2026, 6, 27) && r.Date <= new DateTime(2026, 7, 10))
                        .OrderBy(r => r.Date)
                        .ToListAsync();
                    foreach (var r in records)
                    {
                        sb.AppendLine($"| {r.Date:yyyy/MM/dd} | {r.Status} | {r.CheckInTime:HH:mm} | {r.CheckOutTime:HH:mm} | {r.HoursWorked:F2} |");
                    }
                    sb.AppendLine();
                }
            }
            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\mismatch_details_dump.md", sb.ToString());
        }

        [Fact]
        public async Task DumpAllEmployeesDailyHours()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string excelPath = @"c:\Users\Neil\source\repos\OCC\Copy of G. JHB 10 JUL 26 (003).xlsx";
            var excelList = new List<ExcelEmployeeDetails>();

            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables["OCC"] ?? result.Tables[0];
                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    string col1 = row[1]?.ToString()?.Trim() ?? ""; // BAS
                    string col2 = row[2]?.ToString()?.Trim() ?? ""; // NAME
                    if ((int.TryParse(col1, out int basNum) || col1.StartsWith("CAS")) && !string.IsNullOrEmpty(col2) && col2 != "NAME")
                    {
                        excelList.Add(new ExcelEmployeeDetails { Bas = col1, Name = col2, Hours = ParseDouble(row[5]) });
                    }
                }
            }

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(dbOptions);
            var dbEmployees = await context.Employees.Where(e => e.Branch == "Johannesburg").ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("# JHB Employees Daily Hours and Excel Hours Comparison");
            sb.AppendLine();
            sb.AppendLine("| BAS | Name | Excel Hours | DB Actual Hours (Sum) | DB Days Worked |");
            sb.AppendLine("|---|---|---|---|---|");

            foreach (var excelEmp in excelList.OrderBy(e => e.Name))
            {
                var emp = dbEmployees.FirstOrDefault(e => e.EmployeeNumber?.Trim() == excelEmp.Bas);
                if (emp != null)
                {
                    var records = await context.AttendanceRecords
                        .Where(r => r.EmployeeId == emp.Id && r.Date >= new DateTime(2026, 6, 27) && r.Date <= new DateTime(2026, 7, 10))
                        .ToListAsync();
                    
                    double dbSum = records.Sum(r => r.HoursWorked);
                    int daysWorked = records.Count(r => r.HoursWorked > 0 && r.Status == AttendanceStatus.Present);

                    sb.AppendLine($"| {excelEmp.Bas} | {emp.FirstName} {emp.LastName} | {excelEmp.Hours:F2} | {dbSum:F2} | {daysWorked} |");
                }
            }

            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\all_employees_hours_compare.md", sb.ToString());
        }

        [Fact]
        public async Task DumpTimothyDetails()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(dbOptions);
            var emp = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeNumber == "CAS.002");
            
            var sb = new StringBuilder();
            sb.AppendLine("# Timothy details Daily Dump");
            sb.AppendLine();

            if (emp != null)
            {
                sb.AppendLine($"## {emp.FirstName} {emp.LastName} (CAS.002)");
                sb.AppendLine("| Date | Status | Check In | Check Out | Hours |");
                sb.AppendLine("|---|---|---|---|---|");
                var records = await context.AttendanceRecords
                    .Where(r => r.EmployeeId == emp.Id && r.Date >= new DateTime(2026, 6, 27) && r.Date <= new DateTime(2026, 7, 10))
                    .OrderBy(r => r.Date)
                    .ToListAsync();
                foreach (var r in records)
                {
                    sb.AppendLine($"| {r.Date:yyyy/MM/dd} | {r.Status} | {r.CheckInTime:HH:mm} | {r.CheckOutTime:HH:mm} | {r.HoursWorked:F2} |");
                }
                sb.AppendLine();
            }
            File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\timothy_details_dump.md", sb.ToString());
        }

        [Fact]
        public void DumpHerisExcelRow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string excelPath = @"c:\Users\Neil\source\repos\OCC\Copy of G. JHB 10 JUL 26 (003).xlsx";
            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables["OCC"] ?? result.Tables[0];
                var sb = new StringBuilder();

                var headerRow = table.Rows[7];
                sb.AppendLine("## Heris columns:");
                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    if (row[1]?.ToString()?.Trim() == "460")
                    {
                        for (int c = 0; c < table.Columns.Count; c++)
                        {
                            sb.AppendLine($"Col {c} ({headerRow[c]?.ToString()?.Trim()}): '{row[c]?.ToString()?.Trim()}'");
                        }
                    }
                }
                File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\heris_excel_row.txt", sb.ToString());
            }
        }

        [Fact]
        public void DumpExcelColumns()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string excelPath = @"c:\Users\Neil\source\repos\OCC\Copy of G. JHB 10 JUL 26 (003).xlsx";
            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables["OCC"] ?? result.Tables[0];
                var sb = new StringBuilder();

                // Dump header row 7 (which contains column titles)
                var headerRow = table.Rows[7];
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    sb.AppendLine($"Col {c}: '{headerRow[c]?.ToString()?.Trim()}'");
                }

                // Dump row for Andrew Elias Maselela (BAS 459)
                sb.AppendLine("\n--- BAS 459 ---");
                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    if (row[1]?.ToString()?.Trim() == "459")
                    {
                        for (int c = 0; c < table.Columns.Count; c++)
                        {
                            sb.AppendLine($"Col {c} ({headerRow[c]?.ToString()?.Trim()}): '{row[c]?.ToString()?.Trim()}'");
                        }
                    }
                }

                // Dump row for Petros Shitlangu (BAS 338)
                sb.AppendLine("\n--- BAS 338 ---");
                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    if (row[1]?.ToString()?.Trim() == "338")
                    {
                        for (int c = 0; c < table.Columns.Count; c++)
                        {
                            sb.AppendLine($"Col {c} ({headerRow[c]?.ToString()?.Trim()}): '{row[c]?.ToString()?.Trim()}'");
                        }
                    }
                }

                File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\excel_columns_dump.txt", sb.ToString());
            }
        }

        private string EscapeSql(string val)
        {
            if (string.IsNullOrEmpty(val)) return "''";
            return "'" + val.Replace("'", "''") + "'";
        }
    }
}
