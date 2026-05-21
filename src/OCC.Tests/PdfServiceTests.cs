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
                    Status = "Active"
                },
                WeekNumber = 12,
                TotalTasks = 50,
                InProgressTasks = 15,
                CompletedTasks = 30,
                OverallProgress = 0.65f,
                PowPercentRequired = 0.70f,
                DelayDays = 3,
                SafeWorkingHours = 1250,
                SiteEstablishmentPlanned = DateTime.Today.AddDays(-30),
                SiteEstablishmentActual = DateTime.Today.AddDays(-28),
                PracticalCompletionPlanned = DateTime.Today.AddDays(90),
                PracticalCompletionActual = null,
                StreamingPlanned = DateTime.Today.AddDays(45),
                StreamingActual = DateTime.Today.AddDays(44),
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

            // Copy to workspace root for easy user visual inspection
            var targetPath = @"c:\Users\Neil\source\repos\OCC\SampleProjectReport.pdf";
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
            Assert.True(File.Exists(targetPath), "The PDF file was not copied to the workspace root.");
        }
    }
}
