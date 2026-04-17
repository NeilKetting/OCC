using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Services
{
    public partial class PdfService : IPdfService
    {
        public async Task<string> GenerateWageRunPdfAsync(WageRun wageRun)
        {
            var company = await _settingsService.GetCompanyDetailsAsync();

            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(10);
                        page.Size(PageSizes.A4.Landscape());
                        page.DefaultTextStyle(x => x.FontSize(6.5f).FontFamily(Fonts.Arial).FontColor(Colors.Black));

                        page.Header().Element(c => ComposeWageHeader(c, wageRun, company));
                        page.Content().PaddingVertical(5).Element(c => ComposeWageContent(c, wageRun));
                        page.Footer().PaddingTop(5).Element(c => ComposeWageFooter(c, company));
                    });
                });

                string docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OCC", "WageRuns");
                if (!Directory.Exists(docsPath)) Directory.CreateDirectory(docsPath);
                
                string filename = $"WageRun_{wageRun.Branch}_{wageRun.EndDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf";
                string fullPath = Path.Combine(docsPath, filename);

                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        private void ComposeWageHeader(IContainer container, WageRun wageRun, CompanyDetails company)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(company.CompanyName.ToUpper() + " Ltd").FontSize(10).ExtraBold();
                    row.RelativeItem().AlignCenter().Text("STAFF WAGES (OCC)").FontSize(10).SemiBold();
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Date: ").SemiBold();
                        t.Span(wageRun.EndDate.ToString("dd/MM/yyyy")).Underline();
                    });
                });
            });
        }

        private void ComposeWageContent(IContainer container, WageRun wageRun)
        {
            container.Column(col =>
            {
                var allLines = wageRun.Lines.OrderBy(l => l.EmployeeName).ToList();

                if (allLines.Any())
                {
                    col.Item().PaddingTop(5).Text("OCC STAFF WAGES").FontSize(8).ExtraBold();
                    col.Item().Element(c => ComposeWageTable(c, allLines));
                }

                // Add Summary Tables at the bottom (Loans, Totals)
                col.Item().PaddingTop(20).Row(row =>
                {
                    row.ConstantItem(150).Element(c => ComposeLoanSummary(c, wageRun));
                    row.RelativeItem();
                    row.ConstantItem(300).Element(c => ComposeWageTotalsTable(c, wageRun));
                });
            });
        }

        private void ComposeWageTable(IContainer container, List<WageRunLine> lines)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(12);  // #
                    columns.ConstantColumn(22);  // BAS
                    columns.RelativeColumn(2.2f); // NAME
                    columns.ConstantColumn(28);  // RATE P/HR
                    columns.ConstantColumn(20);  // HRS
                    columns.ConstantColumn(28);  // STD O/T RATE
                    columns.ConstantColumn(28);  // SAT O/T RATE
                    columns.ConstantColumn(28);  // SUN P/H RATE
                    columns.ConstantColumn(20);  // STD O/T (HRS)
                    columns.ConstantColumn(20);  // SAT O/T (HRS)
                    columns.ConstantColumn(20);  // SUN O/T (HRS)
                    columns.ConstantColumn(28);  // LOANS
                    columns.ConstantColumn(28);  // WASHING
                    columns.ConstantColumn(28);  // GAS
                    columns.ConstantColumn(28);  // OTHER
                    columns.ConstantColumn(35);  // TOTAL NETT
                    columns.ConstantColumn(35);  // BANK
                    columns.RelativeColumn(1.8f); // COMMENTS
                    columns.ConstantColumn(35);  // TOTAL REM
                    columns.ConstantColumn(30);  // RATE P/DAY
                    columns.ConstantColumn(20);  // W1
                    columns.ConstantColumn(20);  // W2
                    columns.ConstantColumn(20);  // TOT D
                    columns.ConstantColumn(22);  // H/D
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderStyle).Text("#");
                    header.Cell().Element(HeaderStyle).Text("BAS");
                    header.Cell().Element(HeaderStyle).Text("NAME");
                    header.Cell().Element(HeaderStyle).Text("RATE\nP/HR");
                    header.Cell().Element(HeaderStyle).Text("HRS");
                    header.Cell().Element(HeaderStyle).Text("STD O/T\nRATE");
                    header.Cell().Element(HeaderStyle).Text("SAT O/T\nRATE");
                    header.Cell().Element(HeaderStyle).Text("SUN P/H\nRATE");
                    header.Cell().Element(HeaderStyle).Text("STD\nO/T");
                    header.Cell().Element(HeaderStyle).Text("SAT\nO/T");
                    header.Cell().Element(HeaderStyle).Text("SUN\nO/T");
                    header.Cell().Element(HeaderStyle).Text("LOANS");
                    header.Cell().Element(HeaderStyle).Text("WASH-ING");
                    header.Cell().Element(HeaderStyle).Text("GAS");
                    header.Cell().Element(HeaderStyle).Text("OTHER");
                    header.Cell().Element(HeaderStyle).Text("TOTAL\nNETT");
                    header.Cell().Element(HeaderStyle).Text("BANK");
                    header.Cell().Element(HeaderStyle).Text("COMMENTS");
                    header.Cell().Element(HeaderStyle).Text("TOTAL\nREM");
                    header.Cell().Element(HeaderStyle).Text("RATE\nP/DAY");
                    header.Cell().Element(HeaderStyle).Text("W1");
                    header.Cell().Element(HeaderStyle).Text("W2");
                    header.Cell().Element(HeaderStyle).Text("TOT\nD");
                    header.Cell().Element(HeaderStyle).Text("H/D");

                    static IContainer HeaderStyle(IContainer container) => 
                        container.Border(0.5f).Background(Colors.Grey.Lighten4).Padding(1).AlignCenter().AlignMiddle().DefaultTextStyle(x => x.Bold().FontSize(5.5f));
                });

                int index = 1;
                foreach (var line in lines)
                {
                    table.Cell().Element(CellStyle).Text(index++.ToString());
                    table.Cell().Element(CellStyle).Text(line.EmployeeNumber ?? "");
                    table.Cell().Element(CellStyle).Text(line.EmployeeName ?? "");
                    table.Cell().Element(CellStyle).AlignRight().Text(line.HourlyRate.ToString("F2"));
                    
                    // Standard Hours = Normal + Projected + Variance
                    decimal stdHours = (decimal)(line.NormalHours + line.ProjectedHours + line.VarianceHours);
                    table.Cell().Element(CellStyle).AlignCenter().Text(stdHours.ToString("F2"));
                    
                    table.Cell().Element(CellStyle).AlignRight().Text((line.HourlyRate * 1.5m).ToString("F2"));
                    table.Cell().Element(CellStyle).AlignRight().Text((line.HourlyRate * 1.5m).ToString("F2"));
                    table.Cell().Element(CellStyle).AlignRight().Text((line.HourlyRate * 2.0m).ToString("F2"));
                    
                    table.Cell().Element(CellStyle).AlignCenter().Text(line.Overtime15Hours.ToString("F2"));
                    table.Cell().Element(CellStyle).AlignCenter().Text("0.00"); 
                    table.Cell().Element(CellStyle).AlignCenter().Text(line.Overtime20Hours.ToString("F2"));

                    table.Cell().Element(CellStyle).AlignRight().Text(line.DeductionLoan.ToString("F2"));
                    table.Cell().Element(CellStyle).AlignRight().Text(line.DeductionWashing.ToString("F2"));
                    table.Cell().Element(CellStyle).AlignRight().Text(line.DeductionGas.ToString("F2"));
                    
                    // OTHER column in Excel includes PPE + regular Other
                    decimal otherTotal = line.DeductionOther + line.DeductionPPE;
                    table.Cell().Element(CellStyle).AlignRight().Text(otherTotal.ToString("F2"));

                    table.Cell().Element(CellStyle).AlignRight().Text(line.NetPay.ToString("F2")).SemiBold();
                    table.Cell().Element(CellStyle).Text(line.BankName ?? "");
                    
                    var comments = line.VarianceNotes ?? "";
                    if (line.IncentiveSupervisor > 0) comments = "SUPERVISOR FEE " + comments;
                    table.Cell().Element(CellStyle).Text(comments);

                    // --- NEW COLUMNS ---
                    decimal totalRem = line.TotalWage;
                    table.Cell().Element(CellStyle).AlignRight().Text(totalRem.ToString("F2"));
                    
                    table.Cell().Element(CellStyle).AlignRight().Text((line.HourlyRate * 8).ToString("F2"));
                    table.Cell().Element(CellStyle).AlignCenter().Text(line.DaysWorkedWeek1.ToString("0"));
                    table.Cell().Element(CellStyle).AlignCenter().Text(line.DaysWorkedWeek2.ToString("0"));
                    table.Cell().Element(CellStyle).AlignCenter().Text(line.TotalDaysWorked.ToString("0"));
                    
                    double totalHrs = line.NormalHours + line.Overtime15Hours + line.Overtime20Hours + line.ProjectedHours;
                    double hpd = line.TotalDaysWorked > 0 ? totalHrs / line.TotalDaysWorked : 0;
                    table.Cell().Element(CellStyle).AlignCenter().Text(hpd.ToString("F1"));

                    static IContainer CellStyle(IContainer container) => 
                        container.Border(0.5f).Padding(1).AlignMiddle();
                }

                // Subtotal for this group
                table.Footer(footer =>
                {
                    footer.Cell().ColumnSpan(15).Element(c => c.AlignRight().PaddingRight(5).Text("TOTAL:").Bold());
                    footer.Cell().Element(c => c.Border(0.5f).Padding(1).AlignRight().Text(lines.Sum(x => x.NetPay).ToString("F2")).Bold());
                    footer.Cell().ColumnSpan(8).Element(c => c.Border(0.5f));
                });
            });
        }

        private void ComposeLoanSummary(IContainer container, WageRun wageRun)
        {
            container.Column(col =>
            {
                col.Item().PaddingBottom(2).Text("LOANS DESCRIPTION").FontSize(7).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(40);
                    });
                    
                    for (int i = 1; i <= 5; i++)
                    {
                        table.Cell().Border(0.5f).Height(10);
                        table.Cell().Border(0.5f).Height(10);
                    }
                });
            });
        }

        private void ComposeWageTotalsTable(IContainer container, WageRun wageRun)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(60);
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => c.Border(0.5f));
                    header.Cell().Element(Style).Text("LOANS");
                    header.Cell().Element(Style).Text("WASHING");
                    header.Cell().Element(Style).Text("GAS");
                    header.Cell().Element(Style).Text("LIVING OUT");
                    header.Cell().Element(Style).Text("TOTAL");

                    static IContainer Style(IContainer container) => container.Border(0.5f).AlignCenter().DefaultTextStyle(x => x.Bold());
                });

                var permLines = wageRun.Lines.Where(l => l.EmploymentType == "Permanent").ToList();
                var casualLines = wageRun.Lines.Where(l => l.EmploymentType != "Permanent").ToList();

                AddTotalRow(table, "Permanent Staff", permLines);
                AddTotalRow(table, "Casual Staff", casualLines);

                // Grand Total
                table.Cell().Element(TotalStyle).Text("Total").Bold();
                table.Cell().Element(TotalStyle).AlignRight().Text(wageRun.Lines.Sum(x => x.DeductionLoan).ToString("F2")).Bold();
                table.Cell().Element(TotalStyle).AlignRight().Text(wageRun.Lines.Sum(x => x.DeductionWashing).ToString("F2")).Bold();
                table.Cell().Element(TotalStyle).AlignRight().Text(wageRun.Lines.Sum(x => x.DeductionGas).ToString("F2")).Bold();
                table.Cell().Element(TotalStyle).AlignRight().Text("0.00").Bold();
                table.Cell().Element(TotalStyle).Background(Colors.Grey.Lighten3).AlignRight().Text(wageRun.Lines.Sum(x => x.NetPay).ToString("F2")).Bold();

                static void AddTotalRow(TableDescriptor table, string label, List<WageRunLine> lines)
                {
                    table.Cell().Element(TotalStyle).Text(label);
                    table.Cell().Element(TotalStyle).AlignRight().Text(lines.Sum(x => x.DeductionLoan).ToString("F2"));
                    table.Cell().Element(TotalStyle).AlignRight().Text(lines.Sum(x => x.DeductionWashing).ToString("F2"));
                    table.Cell().Element(TotalStyle).AlignRight().Text(lines.Sum(x => x.DeductionGas).ToString("F2"));
                    table.Cell().Element(TotalStyle).AlignRight().Text("0.00");
                    table.Cell().Element(TotalStyle).AlignRight().Text(lines.Sum(x => x.NetPay).ToString("F2"));
                }

                static IContainer TotalStyle(IContainer container) => container.Border(0.5f).PaddingHorizontal(2).AlignMiddle();
            });
        }

        private void ComposeWageFooter(IContainer container, CompanyDetails company)
        {
            container.Row(row =>
            {
                row.RelativeItem().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
                row.RelativeItem().AlignRight().Text($"Generated on {DateTime.Now:F} - {company.CompanyName}");
            });
        }
    }
}
