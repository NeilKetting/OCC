using System;
using System.IO;
using OCC.WpfClient.Features.HseqHub.Services;
using Xunit;

namespace OCC.Tests.Features.HseqHub
{
    public class AuditPdfParserTests
    {
        [Fact]
        public void ExtractRowsFromPdf_ParsesEngenChartBasedPdfSuccessfully()
        {
            // Arrange
            string pdfPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "Engen North Ridge Audit 2.pdf");
            pdfPath = Path.GetFullPath(pdfPath);

            if (!File.Exists(pdfPath))
            {
                pdfPath = "Engen North Ridge Audit 2.pdf";
            }

            if (!File.Exists(pdfPath))
            {
                return; // Skip if file not present on runner
            }

            // Act
            var rows = AuditPdfParser.ExtractRowsFromPdf(pdfPath);

            // Assert
            Assert.NotNull(rows);
            Assert.True(rows.Count >= 10);
            Assert.Contains(rows, r => r.PdfCategoryName.Contains("Administrative Requirements", StringComparison.OrdinalIgnoreCase) && r.AchievedScore == 82);
            Assert.Contains(rows, r => r.PdfCategoryName.Contains("Education Training & Promotion", StringComparison.OrdinalIgnoreCase) && r.AchievedScore == 83);
            Assert.Contains(rows, r => r.PdfCategoryName.Contains("Housekeeping", StringComparison.OrdinalIgnoreCase) && r.AchievedScore == 79);
        }
    }
}
