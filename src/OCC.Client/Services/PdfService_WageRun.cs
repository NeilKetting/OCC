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
        public async Task<string> GenerateWageRunPdfAsync(WageRun wageRun, bool hideAfterComments = false)
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
                        page.DefaultTextStyle(x => x.FontSize(4.8f).FontFamily(Fonts.Arial).FontColor(Colors.Black));

                        page.Header().Element(c => ComposeWageHeader(c, wageRun, company));
                        page.Content().PaddingVertical(5).Element(c => ComposeWageContent(c, wageRun, hideAfterComments));
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

        public async Task<string> GenerateSupervisorChecklistPdfAsync(WageRun wageRun)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(20);
                        page.Size(PageSizes.A4.Portrait());
                        page.DefaultTextStyle(x => x.FontSize(8).FontFamily(Fonts.Arial).FontColor(Colors.Black));

                        page.Header().Element(c => 
                        {
                            c.Column(col =>
                            {
                                col.Item().Row(row =>
                                {
                                    row.RelativeItem().Text("ORANGE CIRCLE CONSTRUCTION Ltd").FontSize(12).ExtraBold();
                                    row.RelativeItem().AlignRight().Text("SUPERVISOR CASH PAYMENTS").FontSize(12).SemiBold();
                                });
                                col.Item().PaddingTop(4).Row(row =>
                                {
                                    row.RelativeItem().Text($"Branch: {wageRun.Branch} | Date: {wageRun.EndDate:dd/MM/yyyy}");
                                    row.RelativeItem().AlignRight().Text($"Period: {wageRun.StartDate:dd MMM} - {wageRun.EndDate:dd MMM yyyy}");
                                });
                                col.Item().PaddingVertical(8).LineHorizontal(1f).LineColor(Colors.Grey.Lighten1);
                            });
                        });

                        page.Content().PaddingVertical(5).Element(c => 
                        {
                            c.Column(col =>
                            {
                                col.Item().PaddingBottom(5).Text("Payment Checklist").FontSize(10).Bold();
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(30);  // [ ] Paid
                                        columns.ConstantColumn(40);  // BAS
                                        columns.RelativeColumn(2f);  // NAME
                                        columns.ConstantColumn(70);  // BANK
                                        columns.ConstantColumn(90);  // BANK ACC
                                        columns.ConstantColumn(60);  // SUP FEE
                                        columns.ConstantColumn(120); // SIGNATURE
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HeaderStyle).Text("PAID");
                                        header.Cell().Element(HeaderStyle).Text("BAS");
                                        header.Cell().Element(HeaderStyle).Text("NAME");
                                        header.Cell().Element(HeaderStyle).Text("BANK");
                                        header.Cell().Element(HeaderStyle).Text("BANK ACC");
                                        header.Cell().Element(HeaderStyle).Text("SUP FEE");
                                        header.Cell().Element(HeaderStyle).Text("SIGNATURE");

                                        static IContainer HeaderStyle(IContainer container) => 
                                            container.Border(0.5f).Background(Colors.Grey.Lighten4).Padding(4).AlignCenter().AlignMiddle().DefaultTextStyle(x => x.Bold().FontSize(8));
                                    });

                                    var supervisorLines = wageRun.Lines.Where(l => l.IncentiveSupervisor > 0).OrderBy(l => l.EmployeeName).ToList();
                                    foreach (var line in supervisorLines)
                                    {
                                        table.Cell().Element(CellStyle).AlignCenter().Text("[   ]").FontSize(10);
                                        table.Cell().Element(CellStyle).Text(line.EmployeeNumber ?? "");
                                        table.Cell().Element(CellStyle).Text(line.EmployeeName ?? "");
                                        table.Cell().Element(CellStyle).Text(line.BankName ?? "");
                                        table.Cell().Element(CellStyle).Text(line.BankAccountNumber ?? "");
                                        table.Cell().Element(CellStyle).AlignRight().Text($"R {line.IncentiveSupervisor:F2}").SemiBold();
                                        table.Cell().Element(CellStyle).Height(20);

                                        static IContainer CellStyle(IContainer container) => 
                                            container.Border(0.5f).Padding(4).AlignMiddle();
                                    }

                                    table.Footer(footer =>
                                    {
                                        footer.Cell().ColumnSpan(5).Element(c => c.AlignRight().PaddingRight(5).Text("TOTAL:").Bold());
                                        footer.Cell().Element(c => c.Border(0.5f).Padding(4).AlignRight().Text($"R {supervisorLines.Sum(x => x.IncentiveSupervisor):F2}").Bold());
                                        footer.Cell().Element(c => c.Border(0.5f));
                                    });
                                });
                            });
                        });

                        page.Footer().AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(8);
                            t.CurrentPageNumber().FontSize(8);
                            t.Span(" of ").FontSize(8);
                            t.TotalPages();
                        });
                    });
                });

                string docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OCC", "WageRuns");
                if (!Directory.Exists(docsPath)) Directory.CreateDirectory(docsPath);

                string filename = $"SupervisorPayments_{wageRun.Branch}_{wageRun.EndDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf";
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
                    row.RelativeItem().Text(company.CompanyName.ToUpper() + " Ltd").FontSize(8).ExtraBold();
                    row.RelativeItem().AlignCenter().Text("STAFF WAGES (OCC)").FontSize(8).SemiBold();
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Date: ").SemiBold().FontSize(8);
                        t.Span(wageRun.EndDate.ToString("dd/MM/yyyy")).Underline().FontSize(8);
                    });
                });
            });
        }

        private void ComposeWageContent(IContainer container, WageRun wageRun, bool hideAfterComments)
        {
            container.Column(col =>
            {
                var allLines = wageRun.Lines.OrderBy(l => l.EmployeeName).ToList();

                if (allLines.Any())
                {
                    col.Item().PaddingTop(5).Text("OCC STAFF WAGES").FontSize(6).ExtraBold();
                    col.Item().Element(c => ComposeWageTable(c, allLines, hideAfterComments));
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

        private void ComposeWageTable(IContainer container, List<WageRunLine> lines, bool hideAfterComments)
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
                    columns.ConstantColumn(28);  // DEC O/T RATE
                    columns.ConstantColumn(20);  // DEC O/T HRS
                    columns.ConstantColumn(28);  // DEC TOTAL
                    columns.ConstantColumn(20);  // STD O/T (HRS)
                    columns.ConstantColumn(20);  // SAT O/T (HRS)
                    columns.ConstantColumn(20);  // SUN O/T (HRS)
                    columns.ConstantColumn(28);  // LOANS
                    columns.ConstantColumn(28);  // WASHING
                    columns.ConstantColumn(28);  // GAS
                    columns.ConstantColumn(28);  // OTHER
                    columns.ConstantColumn(35);  // TOTAL NETT
                    columns.ConstantColumn(35);  // BANK
                    columns.ConstantColumn(60);  // BANK ACC
                    columns.RelativeColumn(1.8f); // COMMENTS
                    columns.RelativeColumn(1.5f); // NOTES
                    
                    if (!hideAfterComments)
                    {
                        columns.ConstantColumn(35);  // TOTAL REM
                        columns.ConstantColumn(30);  // RATE P/DAY
                        columns.ConstantColumn(16);  // W1
                        columns.ConstantColumn(16);  // W2
                        columns.ConstantColumn(16);  // W3
                        columns.ConstantColumn(20);  // TOT D
                    }
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
                    header.Cell().Element(HeaderStyle).Text("SUN-P'HOL\nRATE");
                    header.Cell().Element(HeaderStyle).Text("DEC O/T\nRATE");
                    header.Cell().Element(HeaderStyle).Text("DEC O/T\nHRS");
                    header.Cell().Element(HeaderStyle).Text("DEC\nTOTAL");
                    header.Cell().Element(HeaderStyle).Text("STD\nO/T");
                    header.Cell().Element(HeaderStyle).Text("SAT\nO/T");
                    header.Cell().Element(HeaderStyle).Text("SUN\nO/T");
                    header.Cell().Element(HeaderStyle).Text("LOANS");
                    header.Cell().Element(HeaderStyle).Text("WASH-ING");
                    header.Cell().Element(HeaderStyle).Text("GAS");
                    header.Cell().Element(HeaderStyle).Text("OTHER");
                    header.Cell().Element(HeaderStyle).Text("TOTAL\nNETT");
                    header.Cell().Element(HeaderStyle).Text("BANK");
                    header.Cell().Element(HeaderStyle).Text("ACCOUNT\nNUMBER");
                    header.Cell().Element(HeaderStyle).Text("COMMENTS");
                    header.Cell().Element(HeaderStyle).Text("NOTES");

                    if (!hideAfterComments)
                    {
                        header.Cell().Element(HeaderStyle).Text("TOTAL\nREM");
                        header.Cell().Element(HeaderStyle).Text("RATE\nP/DAY");
                        header.Cell().Element(HeaderStyle).Text("WEEK 1");
                        header.Cell().Element(HeaderStyle).Text("WEEK 2");
                        header.Cell().Element(HeaderStyle).Text("WEEK 3");
                        header.Cell().Element(HeaderStyle).Text("TOTAL\nDAYS");
                    }

                    static IContainer HeaderStyle(IContainer container) => 
                        container.Border(0.5f).Background(Colors.Grey.Lighten4).Padding(1).AlignCenter().AlignMiddle().DefaultTextStyle(x => x.Bold().FontSize(4.0f));
                });

                int index = 1;
                foreach (var line in lines)
                {
                    table.Cell().Element(CellStyle).Text(index++.ToString());
                    table.Cell().Element(CellStyle).Text(line.EmployeeNumber ?? "");
                    table.Cell().Element(CellStyle).Text(line.EmployeeName ?? "");
                    table.Cell().Element(CellStyle).AlignRight().Text(line.HourlyRate.ToString("F2"));
                    
                    decimal stdHours = (decimal)(line.NormalHours + line.ProjectedHours + line.VarianceHours);
                    table.Cell().Element(CellStyle).AlignCenter().Text(stdHours.ToString("F2"));
                    
                    table.Cell().Element(CellStyle).AlignRight().Text((line.HourlyRate * 1.5m).ToString("F2"));
                    table.Cell().Element(CellStyle).AlignRight().Text((line.HourlyRate * 1.5m).ToString("F2"));
                    table.Cell().Element(CellStyle).AlignRight().Text((line.HourlyRate * 2.0m).ToString("F2"));

                    table.Cell().Element(CellStyle).AlignRight().Text(line.HourlyRate.ToString("F2")); // DEC O/T RATE
                    table.Cell().Element(CellStyle).AlignCenter().Text("0.00");                       // DEC O/T HRS
                    table.Cell().Element(CellStyle).AlignRight().Text("0.00");                        // DEC TOTAL
                    
                    table.Cell().Element(CellStyle).AlignCenter().Text(line.Overtime15Hours.ToString("F2"));
                    table.Cell().Element(CellStyle).AlignCenter().Text("0.00"); 
                    table.Cell().Element(CellStyle).AlignCenter().Text(line.Overtime20Hours.ToString("F2"));

                    table.Cell().Element(CellStyle).AlignRight().Text(line.DeductionLoan.ToString("F2"));
                    table.Cell().Element(CellStyle).AlignRight().Text(line.DeductionWashing.ToString("F2"));
                    table.Cell().Element(CellStyle).AlignRight().Text(line.DeductionGas.ToString("F2"));
                    
                    decimal otherTotal = line.DeductionOther + line.DeductionPPE;
                    table.Cell().Element(CellStyle).AlignRight().Text(otherTotal.ToString("F2"));

                    table.Cell().Element(CellStyle).AlignRight().Text(line.NetPay.ToString("F2")).SemiBold();
                    table.Cell().Element(CellStyle).Text(line.BankName ?? "");
                    table.Cell().Element(CellStyle).Text(line.BankAccountNumber ?? "");
                    
                    table.Cell().Element(CellStyle).Text(line.Comments ?? "");
                    table.Cell().Element(CellStyle).Text(line.VarianceNotes ?? "");

                    if (!hideAfterComments)
                    {
                        table.Cell().Element(CellStyle).AlignRight().Text(line.TotalWage.ToString("F2"));
                        table.Cell().Element(CellStyle).AlignRight().Text((line.HourlyRate * 8.75m).ToString("F2"));
                        table.Cell().Element(CellStyle).AlignCenter().Text(line.DaysWorkedWeek1.ToString("0.#"));
                        table.Cell().Element(CellStyle).AlignCenter().Text(line.DaysWorkedWeek2.ToString("0"));
                        table.Cell().Element(CellStyle).AlignCenter().Text(line.DaysWorkedWeek3.ToString("0"));
                        table.Cell().Element(CellStyle).AlignCenter().Text(line.TotalDaysWorked.ToString("0"));
                    }

                    if (line.IncentiveSupervisor > 0)
                    {
                        for (int i = 0; i < 17; i++)
                        {
                            table.Cell().Element(SubRowStyle);
                        }

                        table.Cell().Element(SubRowStyle).AlignRight().Text("SUPERVISOR FEE").Bold().FontSize(4.0f);
                        table.Cell().Element(SubRowStyle).AlignRight().Text($"R {line.IncentiveSupervisor:F2}").Bold().FontSize(4.0f);

                        int remCount = hideAfterComments ? 4 : 10;
                        for (int i = 0; i < remCount; i++)
                        {
                            table.Cell().Element(SubRowStyle);
                        }
                    }
                    
                    static IContainer CellStyle(IContainer container) => 
                        container.Border(0.5f).Padding(1).AlignMiddle();

                    static IContainer SubRowStyle(IContainer container) => 
                        container.BorderLeft(0.5f).BorderRight(0.5f).BorderBottom(0.5f).Background(Colors.Grey.Lighten3).Padding(1).AlignMiddle();
                }

                // Subtotal for this group
                table.Footer(footer =>
                {
                    footer.Cell().ColumnSpan(18).Element(c => c.AlignRight().PaddingRight(5).Text("TOTAL:").Bold());
                    footer.Cell().Element(c => c.Border(0.5f).Padding(1).AlignRight().Text(lines.Sum(x => x.NetPay).ToString("F2")).Bold());
                    
                    uint footSpan = hideAfterComments ? 4u : 10u;
                    footer.Cell().ColumnSpan(footSpan).Element(c => c.Border(0.5f));
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
            container.Column(col =>
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(55);
                        columns.ConstantColumn(55);
                        columns.ConstantColumn(55);
                        columns.ConstantColumn(55);
                        columns.ConstantColumn(65);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(c => c.Border(0.5f));
                        header.Cell().Element(WageTotStyle).Text("LOANS");
                        header.Cell().Element(WageTotStyle).Text("WASHING");
                        header.Cell().Element(WageTotStyle).Text("GAS");
                        header.Cell().Element(WageTotStyle).Text("LIVING OUT");
                        header.Cell().Element(WageTotStyle).Text("TOTAL");

                        static IContainer WageTotStyle(IContainer c) =>
                            c.Border(0.5f).AlignCenter().DefaultTextStyle(x => x.Bold().FontSize(6.5f));
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

                    static IContainer TotalStyle(IContainer container) => container.Border(0.5f).PaddingHorizontal(2).AlignMiddle().DefaultTextStyle(x => x.FontSize(6.5f));
                });

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(80);
                    });

                    var permNet = wageRun.Lines.Where(l => l.EmploymentType == "Permanent").Sum(x => x.NetPay);
                    var casualNet = wageRun.Lines.Where(l => l.EmploymentType != "Permanent").Sum(x => x.NetPay);
                    var loansTotal = wageRun.Lines.Sum(x => x.DeductionLoan);
                    var grandTotal = permNet + casualNet - loansTotal;

                    table.Cell().Element(RowStyle).Text("Permanent Staff").Bold();
                    table.Cell().Element(RowStyle).AlignRight().Text(permNet.ToString("R #,##0.00"));

                    table.Cell().Element(RowStyle).Text("Casual / Temp Staff").Bold();
                    table.Cell().Element(RowStyle).AlignRight().Text(casualNet.ToString("R #,##0.00"));

                    table.Cell().Element(RowStyle).Text("Loans").Bold();
                    table.Cell().Element(RowStyle).AlignRight().Text(loansTotal.ToString("R #,##0.00"));

                    table.Cell().Element(RowStyle).Background(Colors.Grey.Lighten3).Text("Total of Wage Run").Bold().FontColor(Colors.Red.Medium);
                    table.Cell().Element(RowStyle).Background(Colors.Grey.Lighten3).AlignRight().Text(grandTotal.ToString("R #,##0.00")).Bold().FontColor(Colors.Red.Medium);

                    static IContainer RowStyle(IContainer c) =>
                        c.Border(0.5f).Padding(2).AlignMiddle().DefaultTextStyle(x => x.FontSize(7.5f));
                });
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
