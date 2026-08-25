using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Interfaces;
using OCC.Shared.Models;
using OCC.Shared.DTOs;

namespace OCC.Tests
{
    public class PdfServiceTests
    {


        [Fact]
        public async Task GenerateProjectReportPdfAsync_ShouldCreatePdfFile()
        {
            // Arrange
            var pdfService = new PdfService();
            
            var model = new ProjectReportPrintModel
            {
                Project = new Project
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Project A",
                    Customer = "Acme Corp",
                    Status = "Active",
                    StartDate = DateTime.Today.AddDays(-30),
                    EndDate = DateTime.Today.AddDays(90)
                },
                WeekNumber = 12,
                TotalTasks = 50,
                InProgressTasks = 15,
                CompletedTasks = 30,
                OverallProgress = 0.65f,
                PowPercentRequired = 0.70f,
                DelayDays = 3,
                SafeWorkingHours = 1250,
                ThisWeekMilestones = new List<MilestonePrintModel>
                {
                    new MilestonePrintModel
                    {
                        Name = "Site Establishment",
                        PlannedDate = DateTime.Today.AddDays(-2),
                        Progress = 100,
                        Status = "Complete",
                        IsComplete = true
                    },
                    new MilestonePrintModel
                    {
                        Name = "Practical Completion",
                        PlannedDate = DateTime.Today.AddDays(2),
                        Progress = 50,
                        Status = "In Progress",
                        IsComplete = false,
                        Reason = "Waiting for inspection approvals"
                    }
                },
                OverdueMilestones = new List<MilestonePrintModel>
                {
                    new MilestonePrintModel
                    {
                        Name = "Streaming (Go-Live)",
                        PlannedDate = DateTime.Today.AddDays(-5),
                        Progress = 80,
                        Status = "Delayed",
                        IsComplete = false,
                        Reason = "Hardware delivery delayed by supplier"
                    }
                },
                GeneralWasteTon = "12.5",
                RubbleM3 = "45.0",
                ScrapMetalsTon = "3.2",
                AsbestosTon = "0.0",
                StatusSummary = "The project is currently tracking slightly behind program due to adverse weather conditions, but mitigation measures are in place.",
                VendorReportRows = new List<ProjectReportPrintVendorRow>
                {
                    new ProjectReportPrintVendorRow
                    {
                        VendorName = "Bricklayers Ltd",
                        Scope = "Bricklaying & Plastering",
                        SafetyApproved = "Yes",
                        AppScore = "85",
                        Audit1 = "A",
                        Audit2 = "B+",
                        Audit3 = "A-"
                    },
                    new ProjectReportPrintVendorRow
                    {
                        VendorName = "Piping Solutions",
                        Scope = "Plumbing Services",
                        SafetyApproved = "No",
                        AppScore = "60",
                        Audit1 = "C",
                        Audit2 = "D",
                        Audit3 = string.Empty
                    }
                },
                VariationOrders = new List<ProjectVariationOrder>
                {
                    new ProjectVariationOrder
                    {
                        Date = DateTime.Today.AddDays(-10),
                        Description = "Additional foundation excavation due to soil conditions",
                        ApprovedBy = "John Doe",
                        Status = "Approved",
                        AdditionalComments = "Standard rate applied."
                    },
                    new ProjectVariationOrder
                    {
                        Date = DateTime.Today.AddDays(-5),
                        Description = "Extra plumbing connections for unit 4",
                        ApprovedBy = "Jane Smith",
                        Status = "Pending",
                        AdditionalComments = "Waiting for formal signature."
                    }
                },
                IncidentPhotoPaths = new List<string>()
            };

            // Act
            var path = await pdfService.GenerateProjectReportPdfAsync(model);

            // Output to temp path for test verification
            var targetPath = Path.Combine(Path.GetTempPath(), "SampleProjectReport.pdf");
            try
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                File.Copy(path, targetPath);
            }
            catch { }

            // Assert
            Assert.True(File.Exists(path), "The PDF file was not generated.");
        }

        [Fact]
        public async Task GenerateModernProjectReportPdfAsync_ShouldCreatePdfFile()
        {
            // Arrange
            var pdfService = new PdfService();
            
            var model = new ProjectReportPrintModel
            {
                Project = new Project
                {
                    Id = Guid.NewGuid(),
                    Name = "Orange Circle Project B",
                    Customer = "Engen",
                    Status = "In Progress",
                    StartDate = DateTime.Today.AddDays(-60),
                    EndDate = DateTime.Today.AddDays(120)
                },
                WeekNumber = 8,
                TotalTasks = 75,
                InProgressTasks = 20,
                CompletedTasks = 45,
                OverallProgress = 62.5,
                PowPercentRequired = 65.0,
                DelayDays = 2,
                SafeWorkingHours = 3450,
                ThisWeekMilestones = new List<MilestonePrintModel>
                {
                    new MilestonePrintModel
                    {
                        Name = "Roof Truss Installation",
                        PlannedDate = DateTime.Today.AddDays(1),
                        Progress = 90,
                        Status = "In Progress",
                        IsComplete = false
                    }
                },
                OverdueMilestones = new List<MilestonePrintModel>(),
                GeneralWasteTon = "18.2",
                RubbleM3 = "60.0",
                ScrapMetalsTon = "5.0",
                AsbestosTon = "0.0",
                StatusSummary = "Work is progressing well on modern executive building layout.",
                VendorReportRows = new List<ProjectReportPrintVendorRow>
                {
                    new ProjectReportPrintVendorRow
                    {
                        VendorName = "Orange Circle Construction",
                        Scope = "Primary Contractor",
                        Audit1 = "100,00%",
                        Audit2 = "94,25%",
                        Audit3 = "99,48%"
                    },
                    new ProjectReportPrintVendorRow
                    {
                        VendorName = "Volt Tech Electrical",
                        Scope = "Electrical Contracting",
                        Audit1 = "90,00%",
                        Audit2 = "92,00%",
                        Audit3 = "-"
                    }
                },
                VariationOrders = new List<ProjectVariationOrder>
                {
                    new ProjectVariationOrder
                    {
                        Date = DateTime.Today.AddDays(-3),
                        Description = "Additional site lighting setup for night shift works",
                        ApprovedBy = "Site Manager",
                        Status = "Approved",
                        AdditionalComments = "Completed as requested."
                    }
                },
                IncidentPhotoPaths = new List<string>()
            };

            // Act
            var path = await pdfService.GenerateModernProjectReportPdfAsync(model);

            // Assert
            Assert.True(File.Exists(path), "The modern executive PDF file was not generated.");
        }

        [Fact]
        public void VendorReportRow_AvgScore_CalculatesCorrectly()
        {
            // Arrange
            var row = new ProjectReportPrintVendorRow
            {
                Audit1 = "100,00%",
                Audit2 = "94,25%",
                Audit3 = "99,48%"
            };

            // Act & Assert
            Assert.Equal("97,91%", row.AvgScore.Replace(".", ","));
        }

        [Fact]
        public async Task GenerateWeeklyAttendanceReportPdfAsync_ShouldCreatePdfFile()
        {
            // Arrange
            var pdfService = new PdfService();
            
            var weeks = new List<WeeklyAttendanceReportWeekModel>
            {
                new WeeklyAttendanceReportWeekModel
                {
                    WeekStart = new DateTime(2026, 6, 20),
                    WeekEnd = new DateTime(2026, 6, 26),
                    Employees = new List<WeeklyAttendancePrintModel>
                    {
                        new WeeklyAttendancePrintModel
                        {
                            EmployeeName = "Aaron Moselane",
                            Days = new DailyAttendancePrintModel[]
                            {
                                new DailyAttendancePrintModel { Site = "Mosselbay", TimeIn = "07:00", TimeOut = "14:00", Overtime = "7.0" }, // Sat
                                new DailyAttendancePrintModel { Site = "", TimeIn = "", TimeOut = "", Overtime = "" }, // Sun
                                new DailyAttendancePrintModel { Site = "Mosselbay", TimeIn = "07:00", TimeOut = "16:45", Overtime = "" }, // Mon
                                new DailyAttendancePrintModel { Site = "Mosselbay", TimeIn = "07:00", TimeOut = "16:45", Overtime = "" }, // Tue
                                new DailyAttendancePrintModel { Site = "Mosselbay", TimeIn = "07:00", TimeOut = "16:45", Overtime = "" }, // Wed
                                new DailyAttendancePrintModel { Site = "", TimeIn = "", TimeOut = "", Overtime = "" }, // Thu
                                new DailyAttendancePrintModel { Site = "", TimeIn = "", TimeOut = "", Overtime = "" } // Fri
                            }
                        },
                        new WeeklyAttendancePrintModel
                        {
                            EmployeeName = "Coster Malepe",
                            Days = new DailyAttendancePrintModel[]
                            {
                                new DailyAttendancePrintModel { Site = "", TimeIn = "", TimeOut = "", Overtime = "" }, // Sat
                                new DailyAttendancePrintModel { Site = "", TimeIn = "", TimeOut = "", Overtime = "" }, // Sun
                                new DailyAttendancePrintModel { Site = "ABSENT", TimeIn = "XXXX", TimeOut = "XXXX", Overtime = "UNP" }, // Mon
                                new DailyAttendancePrintModel { Site = "ABSENT", TimeIn = "XXXX", TimeOut = "XXXX", Overtime = "UNP" }, // Tue
                                new DailyAttendancePrintModel { Site = "ABSENT", TimeIn = "XXXX", TimeOut = "XXXX", Overtime = "UNP" }, // Wed
                                new DailyAttendancePrintModel { Site = "", TimeIn = "", TimeOut = "", Overtime = "" }, // Thu
                                new DailyAttendancePrintModel { Site = "", TimeIn = "", TimeOut = "", Overtime = "" } // Fri
                            }
                        }
                    }
                }
            };

            // Act
            var path = await pdfService.GenerateWeeklyAttendanceReportPdfAsync(
                "Weekly Attendance Register",
                "All Branches",
                "",
                weeks);

            // Output to temp path for test verification
            var targetPath = Path.Combine(Path.GetTempPath(), "SampleWeeklyAttendanceReport.pdf");
            try
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                File.Copy(path, targetPath);
            }
            catch { }

            // Assert
            Assert.True(File.Exists(path), "The PDF file was not generated.");
        }

        [Fact]
        public async Task GenerateEmployeeProfilePdfAsync_ShouldCreatePdfFile()
        {
            // Arrange
            var pdfService = new PdfService();
            var employee = new Employee
            {
                FirstName = "Stuart",
                LastName = "Khoza",
                EmployeeNumber = "EMP469",
                IdType = IdType.Passport,
                IdNumber = "MA586749",
                PermitNumber = null, // Matches the placeholder/empty in screenshot
                DoB = new DateTime(1994, 11, 29),
                TaxNumber = "0588983288",
                LivesInCompanyHousing = false,
                Email = "name@example.com",
                Phone = "053-237-0331",
                PhysicalAddress = "2016 Legwalagwala Street, Mayibuye",
                Role = EmployeeRole.GeneralWorker,
                Status = EmployeeStatus.Active,
                EmploymentType = EmploymentType.Contract,
                ContractDuration = "12 Months",
                Branch = "Johannesburg",
                EmploymentDate = new DateTime(2022, 3, 1),
                AnnualLeaveBalance = 15.0,
                SickLeaveBalance = 30.0,
                RateType = RateType.Hourly,
                HourlyRate = 85.50,
                BankName = "Capitec",
                AccountNumber = "1234567890",
                BranchCode = "470010",
                AccountType = "Savings"
            };

            // Act
            var path = await pdfService.GenerateEmployeeProfilePdfAsync(employee);

            // Output to temp path for test verification
            var targetPath = Path.Combine(Path.GetTempPath(), "SampleEmployeeProfileReport.pdf");
            try
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                File.Copy(path, targetPath);
            }
            catch { }

            // Assert
            Assert.True(File.Exists(path), "The PDF file was not generated.");
        }

        [Fact]
        public async Task GenerateWageRunPdfAsync_ShouldCreatePdfFile()
        {
            // Arrange
            var pdfService = new PdfService();
            var wageRun = new WageRun
            {
                StartDate = new DateTime(2026, 6, 20),
                EndDate = new DateTime(2026, 6, 26),
                Branch = "Johannesburg",
                Lines = new List<WageRunLine>
                {
                    new WageRunLine
                    {
                        EmployeeNumber = "444",
                        EmployeeName = "AARON MOSELANE",
                        EmploymentType = "Permanent",
                        HourlyRate = 33.00m,
                        NormalHours = 78.75,
                        Overtime15Hours = 5.0,
                        SaturdayOvertimeHours = 4.0,
                        Overtime20Hours = 2.0,
                        PublicHolidayOvertimeHours = 7.5,
                        DeductionLoan = 322.50m,
                        TotalWage = 3597.75m,
                        BankName = "Nedbank",
                        BankAccountNumber = "1307940420"
                    },
                    new WageRunLine
                    {
                        EmployeeNumber = "458",
                        EmployeeName = "ALLEN MSIMANGA",
                        EmploymentType = "Permanent",
                        HourlyRate = 33.92m,
                        NormalHours = 70.00,
                        PublicHolidayOvertimeHours = 8.0,
                        TotalWage = 2374.40m,
                        BankName = "Capitec Bank",
                        BankAccountNumber = "1788824397"
                    },
                    new WageRunLine
                    {
                        EmployeeNumber = "331",
                        EmployeeName = "BLONDY MALEPE",
                        EmploymentType = "Casual",
                        HourlyRate = 38.18m,
                        NormalHours = 78.75,
                        IncentiveSupervisor = 500.00m,
                        TotalWage = 3520.26m,
                        BankName = "Nedbank Limited",
                        BankAccountNumber = "1213185792"
                    },
                    new WageRunLine
                    {
                        EmployeeNumber = "122",
                        EmployeeName = "COSTER MALEPE",
                        EmploymentType = "Casual",
                        HourlyRate = 38.18m,
                        NormalHours = 17.50,
                        TotalWage = 667.80m,
                        Comments = "Absent on Mon, Tue, Wed",
                        VarianceNotes = "Variance corrected"
                    }
                }
            };

            // Act - Standard Version
            var pathStandard = await pdfService.GenerateWageRunPdfAsync(wageRun, hideAfterComments: false);
            
            // Act - Filtered Columns Version (with OtHours enabled)
            var visibleCols = new Dictionary<string, bool>
            {
                { "Index", true },
                { "Bas", true },
                { "Name", true },
                { "RateHr", true },
                { "Hrs", true },
                { "OtHours", true },
                { "TotalNett", true }
            };
            var pathFiltered = await pdfService.GenerateWageRunPdfAsync(wageRun, hideAfterComments: false, hideDecColumns: true, visibleColumns: visibleCols);

            // Act - Salary Version
            var pathSalary = await pdfService.GenerateWageRunPdfAsync(wageRun, hideAfterComments: true);

            // Assert
            Assert.True(File.Exists(pathStandard), "Standard Wage Run PDF was not generated.");
            Assert.True(File.Exists(pathFiltered), "Filtered Wage Run PDF was not generated.");
            Assert.True(File.Exists(pathSalary), "Salary Version Wage Run PDF was not generated.");
        }
    }
}
