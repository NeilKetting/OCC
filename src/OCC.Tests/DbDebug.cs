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
            string excelPath = @"c:\Users\Neil\source\repos\OCC\G. JHB 10 JUL 26 (003).xlsx";
            var excelList = new List<ExcelEmployeeDetails>();

            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables["OCC"] ?? result.Tables[0];

                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    string col0 = row[0]?.ToString()?.Trim() ?? "";
                    string col1 = row[1]?.ToString()?.Trim() ?? ""; // BAS
                    string col2 = row[2]?.ToString()?.Trim() ?? ""; // NAME

                    if ((int.TryParse(col1, out int basNum) || col1 == "0" || col1.StartsWith("CAS")) && !string.IsNullOrEmpty(col2) && col2 != "NAME")
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

                        // Check if the next row is a supervisor fee or similar notes row
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
    }
}
