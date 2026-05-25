using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OCC.WpfClient.Services
{
    /// <summary>
    /// Service for generating PDF documents using QuestPDF.
    /// Ported from legacy ComposePremium design to match Orange Circle Construction branding.
    /// </summary>
    public class PdfService : IPdfService
    {
        // Brand Colors (Ported from legacy)
        private static readonly string ColorPrimary = "#EF6C00"; // Orange
        private static readonly string ColorSecondary = "#374151"; // Dark Slate
        private static readonly string ColorLightOrange = "#FFF3E0";

        public PdfService()
        {
            // Initializing QuestPDF with the Community License
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<string> GenerateOrderPdfAsync(Order order, bool isPrintVersion = false)
        {
            // Use hardcoded CompanyDetails for now to match legacy behavior
            var company = new CompanyDetails();

            // Path to save the PDF
            var fileName = $"Order_{order.OrderNumber}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            await Task.Run(() =>
            {
                Document.Create(container =>
                {
                    ComposePremium(container, order, company, isPrintVersion);
                }).GeneratePdf(filePath);
            });

            return filePath;
        }

        #region Premium Design (Legacy)

        private void ComposePremium(IDocumentContainer container, Order order, CompanyDetails company, bool isPrint)
        {
            // Effective Colors
            var effectivePrimary = isPrint ? "#000000" : ColorPrimary;
            var effectiveLight = isPrint ? "#F5F5F5" : ColorLightOrange;

            container.Page(page =>
            {
                page.Margin(0); // Full bleed header handling
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                page.Header().Element(c => ComposePremiumHeader(c, order, company, isPrint));
                page.Content().PaddingHorizontal(20).PaddingVertical(20).Element(c => ComposePremiumContent(c, order, company, isPrint));
                page.Footer().PaddingHorizontal(40).PaddingBottom(20).Element(c => ComposePremiumFooter(c, company));
            });
        }

        private void ComposePremiumHeader(IContainer container, Order order, CompanyDetails company, bool isPrint)
        {
            var effectivePrimary = isPrint ? "#000000" : ColorPrimary;

            container.PaddingTop(20).PaddingHorizontal(20).Row(row =>
            {
                // Left: Title
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("PURCHASE ORDER")
                        .FontSize(20).ExtraBold().FontColor(Colors.Black);
                });
                // Right: Logo and Address
                row.RelativeItem().AlignRight().Column(c =>
                {
                    var logoBytes = GetLogoBytes();
                    if (logoBytes != null)
                    {
                        c.Item().Height(80).AlignRight().Image(logoBytes).FitArea();
                    }
                });
            });
        }

        private byte[]? GetLogoBytes()
        {
            try
            {
                // 1. Try WPF Resource Stream (Best for embedded resources)
                var resourceUri = new Uri("Assets/Images/occ_logo.png", UriKind.Relative);
                var streamInfo = System.Windows.Application.GetResourceStream(resourceUri);
                if (streamInfo == null)
                {
                    // Try .jpg fallback
                    resourceUri = new Uri("Assets/Images/occ_logo.jpg", UriKind.Relative);
                    streamInfo = System.Windows.Application.GetResourceStream(resourceUri);
                }

                if (streamInfo != null)
                {
                    using var ms = new MemoryStream();
                    streamInfo.Stream.CopyTo(ms);
                    return ms.ToArray();
                }

                // 2. Fallback to FileSystem if Resource stream failed
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var locations = new[]
                {
                    Path.Combine(baseDir, "Assets", "Images", "occ_logo.png"),
                    Path.Combine(baseDir, "Assets", "Images", "occ_logo.jpg"),
                    Path.Combine(baseDir, "occ_logo.png"),
                    Path.Combine(baseDir, "occ_logo.jpg"),
                    "Assets/Images/occ_logo.png",
                    "Assets/Images/occ_logo.jpg"
                };

                foreach (var path in locations)
                {
                    if (File.Exists(path))
                    {
                        return File.ReadAllBytes(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logo Load Error: {ex.Message}");
            }
            return null;
        }

        private void ComposePremiumContent(IContainer container, Order order, CompanyDetails company, bool isPrint)
        {
            var branchDetails = company.Branches.ContainsKey(order.Branch)
                ? company.Branches[order.Branch]
                : company.Branches[Branch.JHB];

            var effectivePrimary = isPrint ? "#000000" : ColorPrimary;

            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    // LEFT COLUMN: Supplier -> ShipTo
                    row.ConstantItem(300).Column(col =>
                    {
                        // 1. Supplier Box
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Column(box =>
                        {
                            box.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Supplier").SemiBold();
                            box.Item().Padding(5).Column(details =>
                            {
                                details.Item().Text(order.SupplierName ?? "Unknown Supplier").SemiBold();
                                if (!string.IsNullOrEmpty(order.EntityAddress)) details.Item().Text(order.EntityAddress);
                                if (!string.IsNullOrEmpty(order.Attention)) details.Item().Text(t => { t.Span("Attention: ").Bold(); t.Span(order.Attention); });
                                if (!string.IsNullOrEmpty(order.EntityTel)) details.Item().Text(order.EntityTel);
                            });
                        });

                        col.Item().Height(10);

                        // 2. Ship To Box
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Column(box =>
                        {
                            box.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Ship To / Delivery").SemiBold();
                            box.Item().Padding(5).Column(details =>
                            {
                                details.Item().Text(company.CompanyName).SemiBold();
                                details.Item().Text(branchDetails.AddressLine1);
                                if (!string.IsNullOrEmpty(branchDetails.AddressLine2))
                                    details.Item().Text(branchDetails.AddressLine2);
                                details.Item().Text($"{branchDetails.City}, {branchDetails.PostalCode}");
                            });
                        });
                    });

                    row.ConstantItem(20);

                    // RIGHT COLUMN: Branding Details (Replicated layout from Image 1)
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text(company.CompanyName).Bold();
                        c.Item().AlignRight().Text(branchDetails.AddressLine1);
                        if (!string.IsNullOrEmpty(branchDetails.AddressLine2))
                            c.Item().AlignRight().Text(branchDetails.AddressLine2);
                        c.Item().AlignRight().Text($"{branchDetails.City}, {branchDetails.PostalCode}");

                        c.Item().PaddingTop(8);
                        c.Item().AlignRight().Text(t => { t.Span("Tel: ").SemiBold(); t.Span(branchDetails.Phone); });
                        if (!string.IsNullOrEmpty(branchDetails.Fax))
                            c.Item().AlignRight().Text(t => { t.Span("Fax: ").SemiBold(); t.Span(branchDetails.Fax); });

                        c.Item().PaddingTop(8);
                        foreach (var dept in branchDetails.DepartmentEmails)
                        {
                            c.Item().AlignRight().Text(t => { t.Span($"{dept.Department}: ").SemiBold(); t.Span(dept.EmailAddress); });
                        }

                        c.Item().PaddingTop(10).AlignRight().Column(meta =>
                        {
                            if (!string.IsNullOrEmpty(company.RegistrationNumber))
                                meta.Item().AlignRight().Text(t => { t.Span("Reg No: ").SemiBold(); t.Span(company.RegistrationNumber); });

                            if (!string.IsNullOrEmpty(company.VatNumber))
                                meta.Item().AlignRight().Text(t => { t.Span("VAT No: ").SemiBold(); t.Span(company.VatNumber); });
                        });
                    });
                });

                // Meta Row (Project, SOW, Date, PO)
                column.Item().PaddingTop(20).Row(row =>
                {
                    row.RelativeItem(3).Text(t => { t.Span("PROJECT: ").SemiBold(); t.Span(order.ProjectName ?? "-"); });
                    row.RelativeItem(3).Text(t => { t.Span("SOW: ").SemiBold(); t.Span(string.IsNullOrEmpty(order.ScopeOfWork) ? "-" : order.ScopeOfWork); });
                    row.RelativeItem(1.5f).Text(t => { t.Span("DATE: ").SemiBold(); t.Span($"{order.OrderDate:yyyy-MM-dd}"); });
                    row.RelativeItem(2).AlignRight().Text(t => { t.Span("PO No: ").SemiBold(); t.Span(order.OrderNumber); });
                });

                // Items Table
                column.Item().PaddingTop(5).Element(c => ComposePremiumTable(c, order, isPrint));

                // Bottom: Totals and Instructions
                column.Item().PaddingTop(20).Row(row =>
                {
                    row.ConstantItem(250).Element(c => ComposePremiumDeliveryInstructions(c, order));
                    row.RelativeItem();
                    row.ConstantItem(250).Element(c => ComposePremiumTotals(c, order, isPrint));
                });
            });
        }

        private void ComposePremiumDeliveryInstructions(IContainer container, Order order)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Column(instr =>
            {
                instr.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Delivery Instructions").SemiBold().FontSize(9);
                instr.Item().Padding(5).Text(!string.IsNullOrEmpty(order.DeliveryInstructions) ? order.DeliveryInstructions : "Please contact site manager before delivery.").FontSize(9);
            });
        }

        private void ComposePremiumTable(IContainer container, Order order, bool isPrint)
        {
            var effectivePrimary = isPrint ? "#000000" : ColorPrimary;

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.ConstantColumn(80);
                    columns.RelativeColumn();
                    columns.ConstantColumn(60);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(90);
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => HeaderStyle(c, effectivePrimary)).Text("#");
                    header.Cell().Element(c => HeaderStyle(c, effectivePrimary)).Text("Code");
                    header.Cell().Element(c => HeaderStyle(c, effectivePrimary)).Text("Description");
                    header.Cell().Element(c => HeaderStyle(c, effectivePrimary)).AlignRight().Text("Qty");
                    header.Cell().Element(c => HeaderStyle(c, effectivePrimary)).Text("Unit");
                    header.Cell().Element(c => HeaderStyle(c, effectivePrimary)).AlignRight().Text("Rate");
                    header.Cell().Element(c => HeaderStyle(c, effectivePrimary)).AlignRight().Text("Total");

                    static IContainer HeaderStyle(IContainer container, string color)
                    {
                        return container.Background(color).PaddingVertical(8).PaddingHorizontal(5).DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                    }
                });

                foreach (var line in order.Lines.Where(l => l.QuantityOrdered > 0 || !string.IsNullOrEmpty(l.ItemCode)))
                {
                    var index = order.Lines.IndexOf(line);
                    var background = index % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                    table.Cell().Element(c => CellStyle(c, background)).Text($"{index + 1}");
                    table.Cell().Element(c => CellStyle(c, background)).Text(line.ItemCode);
                    table.Cell().Element(c => CellStyle(c, background)).Text(line.Description);
                    table.Cell().Element(c => CellStyle(c, background)).AlignRight().Text($"{line.QuantityOrdered}");
                    table.Cell().Element(c => CellStyle(c, background)).Text(line.UnitOfMeasure);
                    table.Cell().Element(c => CellStyle(c, background)).AlignRight().Text($"{line.UnitPrice:N2}");
                    table.Cell().Element(c => CellStyle(c, background)).AlignRight().Text($"{line.LineTotal:N2}");
                }

                static IContainer CellStyle(IContainer container, string bg)
                {
                    return container.Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(8).PaddingHorizontal(5);
                }
            });
        }

        private void ComposePremiumTotals(IContainer container, Order order, bool isPrint)
        {
            var effectiveLight = isPrint ? "#F5F5F5" : ColorLightOrange;
            var effectivePrimary = isPrint ? "#000000" : ColorPrimary;

            container.Background(effectiveLight).Padding(15).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Subtotal:").SemiBold().FontColor(ColorSecondary);
                    row.RelativeItem().AlignRight().Text($"{order.SubTotal:N2}").FontColor(ColorSecondary);
                });

                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("VAT (15%):").SemiBold().FontColor(ColorSecondary);
                    row.RelativeItem().AlignRight().Text($"{order.VatTotal:N2}").FontColor(ColorSecondary);
                });

                column.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.White);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("TOTAL").FontSize(14).ExtraBold().FontColor(effectivePrimary);
                    row.RelativeItem().AlignRight().Text($"{order.TotalAmount:N2}").FontSize(14).ExtraBold().FontColor(effectivePrimary);
                });
            });
        }

        private void ComposePremiumFooter(IContainer container, CompanyDetails company)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(20).Row(row =>
                {
                    row.RelativeItem().Column(col => {
                        col.Item().PaddingBottom(5).Text("Authorized Signature").FontSize(9).FontColor(Colors.Grey.Medium);
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    row.ConstantItem(40);

                    row.RelativeItem().Column(col => {
                        col.Item().PaddingBottom(5).Text("Received By").FontSize(9).FontColor(Colors.Grey.Medium);
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });

                    row.RelativeItem().AlignRight().Text($"{DateTime.Now:F} - {company.CompanyName}");
                });
            });
        }

        #endregion

        // Added for Employee Report (Updated branding)
        public async Task<string> GenerateEmployeeReportPdfAsync<T>(Employee employee, DateTime start, DateTime end, IEnumerable<T> data, Dictionary<string, string> summary)
        {
            var company = new CompanyDetails();

            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeReportHeader(c, employee, start, end, company));
                        page.Content().PaddingVertical(20).Element(c => ComposeReportContent(c, employee, data, summary));
                        page.Footer().Element(c => ComposeReportFooter(c, company));
                    });
                });

                string docsPath = Path.GetTempPath();
                string filename = $"Report_{employee.LastName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string fullPath = Path.Combine(docsPath, filename);

                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        private void ComposeReportHeader(IContainer container, Employee employee, DateTime start, DateTime end, CompanyDetails company)
        {
            container.Row(row =>
            {
                row.RelativeItem(3).Column(col =>
                {
                    col.Item().Text(company.CompanyName).FontSize(22).ExtraBold().FontColor(ColorPrimary);
                    col.Item().Text("Staff Performance Report").FontSize(12).FontColor(Colors.Grey.Medium);
                    
                    col.Item().PaddingTop(5).Text(t => 
                    {
                        t.Span("Period: ").FontSize(9).SemiBold();
                        t.Span($"{start:dd MMM yyyy} - {end:dd MMM yyyy}").FontSize(9);
                    });
                });

                row.RelativeItem(2).AlignRight().Column(col =>
                {
                    var logoBytes = GetLogoBytes();
                    if (logoBytes != null)
                    {
                        col.Item().Height(50).AlignRight().Image(logoBytes).FitArea();
                    }
                    col.Item().Text($"Generated: {DateTime.Now:g}").FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }

        private void ComposeReportContent<T>(IContainer container, Employee employee, IEnumerable<T> data, Dictionary<string, string> summary)
        {
            container.Column(col =>
            {
                col.Item().Background(Colors.Grey.Lighten5).Padding(15).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(employee.DisplayName).FontSize(16).Bold().FontColor(ColorSecondary);
                        c.Item().Text(employee.EmployeeNumber ?? "No ID").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text(employee.Branch ?? "No Branch").FontSize(12).SemiBold().FontColor(Colors.Grey.Darken2);
                    });
                });

                col.Item().PaddingTop(20).Element(c => ComposeSummaryGrid(c, summary));
                col.Item().PaddingTop(30).Element(c => ComposeReportTable(c, data));
            });
        }

        private void ComposeSummaryGrid(IContainer container, Dictionary<string, string> summary)
        {
            var totalHours = summary.ContainsKey("Total Hours") ? summary["Total Hours"] : "-";
            var totalOT = summary.ContainsKey("Total Overtime") ? summary["Total Overtime"] : "-";

            string ParseVal(string key, bool isPay)
            {
                if (!summary.ContainsKey(key)) return isPay ? "R0.00" : "0.00";
                var parts = summary[key].Split('|');
                if (parts.Length > 1) return isPay ? parts[1] : parts[0];
                return parts[0];
            }

            var ot15Hours = ParseVal("Overtime (1.5x)", false);
            var ot15Pay = ParseVal("Overtime (1.5x)", true);
            var ot20Hours = ParseVal("Overtime (2.0x)", false);
            var ot20Pay = ParseVal("Overtime (2.0x)", true);

            var lates = summary.ContainsKey("Total Lates") ? summary["Total Lates"] : "0";
            var absences = summary.ContainsKey("Absences") ? summary["Absences"] : "0";
            var pay = summary.ContainsKey("Gross Pay") ? summary["Gross Pay"] : "-";

            var normalPay = summary.ContainsKey("Normal Hours Pay") ? summary["Normal Hours Pay"] : "R0.00";

            container.Row(row =>
            {
                row.RelativeItem().PaddingRight(10).Element(c =>
                {
                    c.Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten3).Padding(10).Column(col =>
                    {
                        col.Item().Text("TOTAL HOURS").FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);
                        col.Item().Text(totalHours).FontSize(14).SemiBold().FontColor(ColorSecondary);
                        col.Item().Text(normalPay).FontSize(10).FontColor(Colors.Grey.Medium);
                    });
                });

                row.RelativeItem(2).PaddingRight(10).Element(c =>
                {
                    c.Border(1).BorderColor(Colors.Grey.Lighten3).Background(Colors.Grey.Lighten5).Padding(10).Column(col =>
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text("OVERTIME").FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);
                            r.RelativeItem().AlignRight().Text(totalOT).FontSize(14).Bold().FontColor(ColorSecondary);
                        });

                        col.Item().PaddingTop(5).Row(r =>
                        {
                            r.RelativeItem().Element(sc => MiniStat(sc, "1.5x", ot15Hours, ot15Pay));
                            r.RelativeItem().Element(sc => MiniStat(sc, "2.0x", ot20Hours, ot20Pay));
                        });
                    });
                });

                row.RelativeItem().PaddingRight(10).Column(col =>
                {
                    col.Item().Element(c => StatCard(c, "LATES", lates, "#FFEBEE", Colors.Red.Darken2));
                    col.Item().PaddingTop(10).Element(c => StatCard(c, "ABSENCES", absences, "#FFEBEE", Colors.Red.Darken2));
                });

                row.RelativeItem().Element(c => StatCard(c, "GROSS PAY", pay, "#FFF3E0", ColorPrimary, true));
            });

            static void StatCard(IContainer c, string title, string value, string bg, string? valueColor = null, bool bold = false)
            {
                c.Background(bg).Border(1).BorderColor(Colors.Grey.Lighten3).Padding(10).Column(col =>
                {
                    col.Item().Text(title).FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);
                    var txt = col.Item().Text(value).FontSize(14);
                    if (bold) txt.Bold(); else txt.SemiBold();
                    if (valueColor != null) txt.FontColor(valueColor); else txt.FontColor(ColorSecondary);
                });
            }

            static void MiniStat(IContainer c, string label, string hours, string pay)
            {
                c.Column(col =>
                {
                    col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().Text(hours).FontSize(10).SemiBold().FontColor(Colors.Grey.Darken3);
                    col.Item().Text(pay).FontSize(9).FontColor(Colors.Grey.Medium);
                });
            }
        }

        private void ComposeReportTable<T>(IContainer container, IEnumerable<T> data)
        {
            // Simplified table for reports
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                
                table.Header(h => {
                    h.Cell().Text("Data");
                });
            });
        }

        private void ComposeReportFooter(IContainer container, CompanyDetails company)
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
        public async Task<string> GenerateListReportPdfAsync<T>(string title, IEnumerable<T> items, List<ReportColumnDefinition> columns)
        {
            var company = new CompanyDetails();

            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeGenericHeader(c, title, company));
                        page.Content().PaddingVertical(20).Element(c => ComposeGenericListContent(c, items, columns));
                        page.Footer().Element(c => ComposeGenericFooter(c, company));
                    });
                });

                string fullPath = Path.Combine(Path.GetTempPath(), $"{title.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        public async Task<string> GenerateDetailReportPdfAsync<T>(string title, T item)
        {
            var company = new CompanyDetails();

            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeGenericHeader(c, title, company));
                        page.Content().PaddingVertical(20).Element(c => ComposeGenericDetailContent(c, item));
                        page.Footer().Element(c => ComposeGenericFooter(c, company));
                    });
                });

                string fullPath = Path.Combine(Path.GetTempPath(), $"Detail_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        private void ComposeGenericHeader(IContainer container, string title, CompanyDetails company)
        {
            container.PaddingTop(20).Row(row =>
            {
                // Left: Title and Company Name
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(company.CompanyName).FontSize(20).ExtraBold().FontColor(ColorPrimary);
                    col.Item().Text(title.ToUpper()).FontSize(14).SemiBold().FontColor(ColorSecondary);
                    
                    col.Item().PaddingTop(5).Text(t => 
                    {
                        t.Span("Date: ").FontSize(9).SemiBold();
                        t.Span($"{DateTime.Now:yyyy-MM-dd}").FontSize(9);
                        t.Span("  Time: ").FontSize(9).SemiBold();
                        t.Span($"{DateTime.Now:HH:mm}").FontSize(9);
                    });
                });

                // Right: Logo
                row.RelativeItem().AlignRight().Column(c =>
                {
                    var logoBytes = GetLogoBytes();
                    if (logoBytes != null)
                    {
                        c.Item().Height(60).AlignRight().Image(logoBytes).FitArea();
                    }
                });
            });
        }

        private void ComposeGenericListContent<T>(IContainer container, IEnumerable<T> items, List<ReportColumnDefinition> columns)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    foreach (var col in columns)
                    {
                        cols.RelativeColumn((float)col.Width);
                    }
                });

                table.Header(header =>
                {
                    foreach (var col in columns)
                    {
                        header.Cell().Background(ColorPrimary).Padding(5).Text(col.Header).SemiBold().FontColor(Colors.White);
                    }
                });

                foreach (var item in items)
                {
                    foreach (var col in columns)
                    {
                        var rawValue = GetRawPropertyValue(item, col.PropertyName);
                        var value = FormatValue(rawValue);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(value);
                    }
                }
            });
        }

        private void ComposeGenericDetailContent<T>(IContainer container, T item)
        {
            if (item == null) return;

            container.Column(col =>
            {
                var properties = item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties)
                {
                    // Filter out non-displayable properties
                    var type = prop.PropertyType;
                    var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

                    if (underlyingType.IsPrimitive || 
                        underlyingType == typeof(string) || 
                        underlyingType == typeof(DateTime) || 
                        underlyingType == typeof(Guid) || 
                        underlyingType == typeof(decimal) ||
                        underlyingType.IsEnum)
                    {
                        var rawValue = prop.GetValue(item);
                        var val = FormatValue(rawValue);
                        if (string.IsNullOrEmpty(val)) val = "-";

                        col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).PaddingVertical(8).Row(row =>
                        {
                            row.ConstantItem(150).Text(ToFriendlyName(prop.Name)).SemiBold().FontColor(Colors.Grey.Darken2);
                            row.RelativeItem().Text(val);
                        });
                    }
                }
            });
        }

        private void ComposeGenericFooter(IContainer container, CompanyDetails company)
        {
            container.PaddingTop(10).BorderTop(1).BorderColor(Colors.Grey.Lighten2).Row(row =>
            {
                row.RelativeItem().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });

                row.RelativeItem().AlignRight().Text($"{company.CompanyName} - Internal Report");
            });
        }

        private string ToFriendlyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            
            // Handle underscores first (e.g. "Contacts_Count" -> "Contacts Count")
            name = name.Replace("_", " ");
            
            // Handle camelcase (e.g. "ContactsCount" -> "Contacts Count")
            // We use a slightly more robust regex to handle cases like "RSAId" -> "RSA Id"
            name = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            name = Regex.Replace(name, "([A-Z])([A-Z][a-z])", "$1 $2");

            return name;
        }

        private string FormatValue(object? value)
        {
            if (value == null) return "-";

            if (value is bool b)
                return b ? "Yes" : "No";

            if (value is DateTime dt)
                return dt.ToString("yyyy-MM-dd");

            if (value is decimal d)
                return d.ToString("N2");

            if (value is double db)
                return db.ToString("N2");

            if (value.GetType().IsEnum)
            {
                return ToFriendlyName(value.ToString() ?? "-");
            }

            return value.ToString() ?? "-";
        }

        private object? GetRawPropertyValue(object? item, string propertyName)
        {
            if (item == null) return null;
            var prop = item.GetType().GetProperty(propertyName);
            return prop?.GetValue(item);
        }

        private string GetPropertyValue(object item, string propertyName)
        {
            return FormatValue(GetRawPropertyValue(item, propertyName));
        }

        public async Task<string> GenerateProjectReportPdfAsync(ProjectReportPrintModel model)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeProjectReportHeader(c, model));
                        page.Content().PaddingVertical(10).Element(c => ComposeProjectReportContent(c, model));
                        page.Footer().Element(c => ComposeProjectReportFooter(c, new CompanyDetails()));
                    });
                });

                string tempPath = Path.GetTempPath();
                string filename = $"ProjectReport_{model.Project.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string fullPath = Path.Combine(tempPath, filename);

                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        private void ComposeProjectReportHeader(IContainer container, ProjectReportPrintModel model)
        {
            container.PaddingBottom(10).Row(row =>
            {
                // Left: OCC Logo
                row.RelativeItem().AlignLeft().Column(col =>
                {
                    var logoBytes = GetLogoBytes();
                    if (logoBytes != null)
                    {
                        col.Item().Height(40).Image(logoBytes).FitArea();
                    }
                    else
                    {
                        col.Item().Text("ORANGE CIRCLE").FontSize(12).ExtraBold().FontColor(ColorPrimary);
                        col.Item().Text("CONSTRUCTION").FontSize(8).Bold().FontColor(ColorSecondary);
                    }
                });

                // Right: Customer Logo or Name
                row.RelativeItem().AlignRight().Column(col =>
                {
                    // 1. If custom customer logo is uploaded and downloaded, render it
                    if (!string.IsNullOrEmpty(model.CustomerLogoPath) && File.Exists(model.CustomerLogoPath))
                    {
                        try
                        {
                            var logoBytes = File.ReadAllBytes(model.CustomerLogoPath);
                            col.Item().Height(40).AlignRight().Image(logoBytes).FitArea();
                            return;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to load customer logo from {model.CustomerLogoPath}: {ex.Message}");
                        }
                    }

                    // 2. Otherwise fall back to pre-defined customers or text fallback
                    var customerName = model.Project.CustomerEntity?.Name ?? model.Project.Customer;
                    if (string.IsNullOrEmpty(customerName))
                    {
                        customerName = "CLIENT REPORT";
                    }

                    if (string.Equals(customerName, "Engen", StringComparison.OrdinalIgnoreCase))
                    {
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Background("#005691").PaddingVertical(4).PaddingHorizontal(12).Text("ENGEN")
                            .FontSize(10).ExtraBold().FontColor(Colors.White).LetterSpacing(0.1f);
                    }
                    else if (string.Equals(customerName, "Vivo Energy", StringComparison.OrdinalIgnoreCase) || string.Equals(customerName, "Vivo", StringComparison.OrdinalIgnoreCase))
                    {
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Background("#E30613").PaddingVertical(4).PaddingHorizontal(12).Text("VIVO ENERGY")
                            .FontSize(10).ExtraBold().FontColor(Colors.White).LetterSpacing(0.1f);
                    }
                    else
                    {
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten3).PaddingVertical(4).PaddingHorizontal(12)
                            .Text(customerName.ToUpper())
                            .FontSize(10).ExtraBold().FontColor(ColorSecondary).LetterSpacing(0.1f);
                    }
                });
            });
        }

        private void ComposeProjectReportContent(IContainer container, ProjectReportPrintModel model)
        {
            container.Column(col =>
            {
                // Title Banner
                col.Item().Background(Colors.Grey.Lighten4).PaddingVertical(8).PaddingHorizontal(12).Row(titleRow =>
                {
                    var titleText = string.IsNullOrEmpty(model.Project.ShortName) 
                        ? $"{model.Project.Name.ToUpper()} PROJECT REPORT" 
                        : $"{model.Project.Name.ToUpper()} - {model.Project.ShortName.ToUpper()} PROJECT REPORT";
                    titleRow.RelativeItem().Text(titleText).FontSize(12).ExtraBold().FontColor(ColorSecondary);
                    titleRow.RelativeItem().AlignRight().Text($"REPORT WEEK {model.WeekNumber}").FontSize(10).Bold().FontColor(ColorPrimary);
                });

                // Dashboard Cards row (Blocks 1-4)
                col.Item().PaddingTop(12).Row(row =>
                {
                    // Block 1: REPORT INFO
                    row.RelativeItem().PaddingRight(4).Element(c => ComposeMetricBlock(c, "REPORT INFO", blockCol =>
                    {
                        blockCol.Item().Text(t => { t.Span("Date: ").SemiBold(); t.Span(model.ReportDate.ToString("yyyy/MM/dd")); });
                        blockCol.Item().Text(t => { t.Span("Week: ").SemiBold(); t.Span(model.WeekNumber.ToString()); });
                        blockCol.Item().Text(t => { t.Span("Status: ").SemiBold(); t.Span(model.Project.Status).Bold().FontColor(GetStatusColor(model.Project.Status)); });
                    }));

                    // Block 2: POW REQUIREMENT
                    row.RelativeItem().PaddingHorizontal(2).Element(c => ComposeMetricBlock(c, "POW PROGRESS", blockCol =>
                    {
                        blockCol.Item().Text(t => { t.Span("Required: ").SemiBold(); t.Span($"{model.PowPercentRequired:F1}%"); });
                        blockCol.Item().Text(t => { t.Span("Actual: ").SemiBold(); t.Span($"{model.OverallProgress:F1}%"); });
                        blockCol.Item().Text(t => { t.Span("Delay: ").SemiBold(); t.Span($"{model.DelayDays} Days").FontColor(model.DelayDays > 0 ? Colors.Red.Darken2 : ColorSecondary); });
                    }));

                    // Block 3: PROGRAM OF WORKS
                    row.RelativeItem().PaddingHorizontal(2).Element(c => ComposeMetricBlock(c, "PROGRAM OF WORKS", blockCol =>
                    {
                        blockCol.Item().Text(t => { t.Span("Total Tasks: ").SemiBold(); t.Span(model.TotalTasks.ToString()); });
                        blockCol.Item().Text(t => { t.Span("Active: ").SemiBold(); t.Span(model.InProgressTasks.ToString()).FontColor(Colors.Blue.Darken2); });
                        blockCol.Item().Text(t => { t.Span("Completed: ").SemiBold(); t.Span(model.CompletedTasks.ToString()).FontColor(Colors.Green.Darken2); });
                    }));

                    // Block 4: SAFE WORKING HOURS
                    row.RelativeItem().PaddingLeft(4).Element(c => ComposeMetricBlock(c, "SAFE WORKING HOURS", blockCol =>
                    {
                        blockCol.Item().AlignCenter().Text($"{model.SafeWorkingHours:N0}").FontSize(14).ExtraBold().FontColor(Colors.Green.Darken3);
                        blockCol.Item().AlignCenter().Text("Safe Hours to Date").FontSize(7.5f).FontColor(Colors.Grey.Medium);
                    }));
                });

                // Block 5: PROJECT STATUS SUMMARY
                col.Item().PaddingTop(10).Element(c => ComposeMetricBlock(c, "PROJECT STATUS SUMMARY", blockCol =>
                {
                    blockCol.Item().Text(model.StatusSummary).FontSize(9).LineHeight(1.15f);
                }));

                 // Milestones & Waste side-by-side
                 col.Item().PaddingTop(12).Row(datesRow =>
                 {
                     // Left: Contract Dates & Milestones Card/Column
                     datesRow.RelativeItem(3).Column(datesCol =>
                     {
                         datesCol.Item().Text("CONTRACT DATES & MILESTONES").FontSize(9).ExtraBold().FontColor(ColorSecondary);
                         
                         // Project Dates Row
                         datesCol.Item().PaddingTop(3).Row(r =>
                         {
                             r.RelativeItem().Text(t => { t.Span("Start: ").FontSize(7.5f).SemiBold(); t.Span(FormatDate(model.Project.StartDate)).FontSize(7.5f); });
                             r.RelativeItem().AlignRight().Text(t => { t.Span("Finish: ").FontSize(7.5f).SemiBold(); t.Span(FormatDate(model.Project.EndDate)).FontSize(7.5f); });
                         });
                         
                         datesCol.Item().PaddingTop(5).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                         
                         // This Week's Milestones Section
                         datesCol.Item().PaddingTop(5).Text("THIS WEEK'S MILESTONES").FontSize(8).ExtraBold().FontColor(ColorSecondary);
                         
                         if (!model.ThisWeekMilestones.Any())
                         {
                             datesCol.Item().PaddingTop(2).Text("No milestones scheduled for this week.").FontSize(7.5f).Italic().FontColor(Colors.Grey.Medium);
                         }
                         else
                         {
                              datesCol.Item().PaddingTop(3).Table(table =>
                              {
                                  table.ColumnsDefinition(cols =>
                                  {
                                      cols.RelativeColumn(3.0f); // Milestone Name
                                      cols.RelativeColumn(1.0f); // Start Date
                                      cols.RelativeColumn(1.0f); // Due Date
                                  });

                                  table.Header(h =>
                                  {
                                      h.Cell().Background(ColorSecondary).Padding(3).Text("Milestone").SemiBold().FontSize(7).FontColor(Colors.White);
                                      h.Cell().Background(ColorSecondary).Padding(3).Text("Start Date").SemiBold().FontSize(7).FontColor(Colors.White);
                                      h.Cell().Background(ColorSecondary).Padding(3).Text("Due Date").SemiBold().FontSize(7).FontColor(Colors.White);
                                  });

                                  foreach (var m in model.ThisWeekMilestones)
                                  {
                                      var nameText = m.Name;
                                      if (m.PlannedDate < DateTime.Today && !m.IsComplete && !string.IsNullOrEmpty(m.Reason))
                                      {
                                          nameText += $"\nReason: {m.Reason}";
                                      }

                                      table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(nameText).FontSize(7);
                                      table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(FormatDate(m.StartDate)).FontSize(7);
                                      table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(FormatDate(m.PlannedDate)).FontSize(7);
                                  }
                              });
                         }

                         // Overdue Milestones Section
                         datesCol.Item().PaddingTop(8).Text("OVERDUE MILESTONES").FontSize(8).ExtraBold().FontColor(ColorSecondary);
                         
                         if (!model.OverdueMilestones.Any())
                         {
                             datesCol.Item().PaddingTop(2).Text("No overdue milestones.").FontSize(7.5f).Italic().FontColor(Colors.Grey.Medium);
                         }
                         else
                         {
                             datesCol.Item().PaddingTop(3).Table(table =>
                             {
                                 table.ColumnsDefinition(cols =>
                                 {
                                     cols.RelativeColumn(3.0f); // Milestone Name + Reason
                                     cols.RelativeColumn(1.1f); // Start Date
                                     cols.RelativeColumn(1.1f); // Due Date
                                     cols.RelativeColumn(0.8f); // Progress
                                 });

                                 table.Header(h =>
                                 {
                                     h.Cell().Background(ColorSecondary).Padding(3).Text("Milestone").SemiBold().FontSize(7).FontColor(Colors.White);
                                     h.Cell().Background(ColorSecondary).Padding(3).Text("Start Date").SemiBold().FontSize(7).FontColor(Colors.White);
                                     h.Cell().Background(ColorSecondary).Padding(3).Text("Due Date").SemiBold().FontSize(7).FontColor(Colors.White);
                                     h.Cell().Background(ColorSecondary).Padding(3).AlignRight().Text("Prog").SemiBold().FontSize(7).FontColor(Colors.White);
                                 });

                                 foreach (var m in model.OverdueMilestones)
                                 {
                                     var nameText = m.Name;
                                     if (!string.IsNullOrEmpty(m.Reason))
                                     {
                                         nameText += $"\nReason: {m.Reason}";
                                     }

                                     table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(nameText).FontSize(7);
                                     table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(FormatDate(m.StartDate)).FontSize(7);
                                     table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(FormatDate(m.PlannedDate)).FontSize(7);
                                     table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignRight().Text($"{m.Progress}%").FontSize(7);
                                 }
                             });
                         }
                     });

                     datesRow.ConstantItem(15);

                    // Right: Waste Disposal Table
                    datesRow.RelativeItem(2).Column(wasteCol =>
                    {
                        wasteCol.Item().Text("ENVIRONMENTAL & WASTE").FontSize(9).ExtraBold().FontColor(ColorSecondary);
                        wasteCol.Item().PaddingTop(3).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.RelativeColumn(0.8f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(ColorSecondary).Padding(4).Text("Waste Type").SemiBold().FontSize(8).FontColor(Colors.White);
                                h.Cell().Background(ColorSecondary).Padding(4).AlignRight().Text("Qty").SemiBold().FontSize(8).FontColor(Colors.White);
                            });

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text("General Waste (T)").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(model.GeneralWasteTon).FontSize(8);

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text("Rubble (m3)").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(model.RubbleM3).FontSize(8);

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text("Scrap Metals (T)").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(model.ScrapMetalsTon).FontSize(8);

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text("Asbestos (T)").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(model.AsbestosTon).FontSize(8);
                        });
                    });
                });

                // Vendor Report
                col.Item().PaddingTop(12).Column(vendorCol =>
                {
                    vendorCol.Item().Text("VENDOR REPORT - HSEQ COMPLIANCE").FontSize(9).ExtraBold().FontColor(ColorSecondary);
                    vendorCol.Item().PaddingTop(3).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2.5f);
                            cols.RelativeColumn(2f);
                            cols.RelativeColumn(1.6f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(ColorSecondary).Padding(4).Text("Vendor Name").SemiBold().FontSize(8).FontColor(Colors.White);
                            h.Cell().Background(ColorSecondary).Padding(4).Text("Scope").SemiBold().FontSize(8).FontColor(Colors.White);
                            h.Cell().Background(ColorSecondary).Padding(4).AlignCenter().Text("Safety File").SemiBold().FontSize(8).FontColor(Colors.White);
                            h.Cell().Background(ColorSecondary).Padding(4).AlignCenter().Text("App Score").SemiBold().FontSize(8).FontColor(Colors.White);
                            h.Cell().Background(ColorSecondary).Padding(4).AlignCenter().Text("AU 1").SemiBold().FontSize(8).FontColor(Colors.White);
                            h.Cell().Background(ColorSecondary).Padding(4).AlignCenter().Text("AU 2").SemiBold().FontSize(8).FontColor(Colors.White);
                            h.Cell().Background(ColorSecondary).Padding(4).AlignCenter().Text("AU 3").SemiBold().FontSize(8).FontColor(Colors.White);
                        });

                        foreach (var row in model.VendorReportRows)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(row.VendorName).FontSize(7.5f);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(row.Scope).FontSize(7.5f);
                            
                            var safetyCell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter();
                            var isSafetyApproved = row.SafetyApproved?.Equals("Yes", StringComparison.OrdinalIgnoreCase) == true;
                            safetyCell.Text(row.SafetyApproved ?? "-").FontSize(7.5f).FontColor(isSafetyApproved ? Colors.Green.Darken2 : Colors.Red.Darken2).Bold();

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(row.AppScore ?? "-").FontSize(7.5f);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(row.Audit1 ?? "-").FontSize(7.5f);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(row.Audit2 ?? "-").FontSize(7.5f);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(row.Audit3 ?? "-").FontSize(7.5f);
                        }
                    });
                });

                // Variation Orders
                col.Item().PaddingTop(12).Column(voCol =>
                {
                    voCol.Item().Text("SITE INSTRUCTIONS / VARIATION ORDERS").FontSize(9).ExtraBold().FontColor(ColorSecondary);
                    
                    if (!model.VariationOrders.Any())
                    {
                        voCol.Item().PaddingTop(3).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text("No site instructions or variation orders recorded.").FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                    }
                    else
                    {
                        voCol.Item().PaddingTop(3).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(3f);
                                cols.RelativeColumn(1.5f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(2f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(ColorSecondary).Padding(4).Text("Date").SemiBold().FontSize(8).FontColor(Colors.White);
                                h.Cell().Background(ColorSecondary).Padding(4).Text("Description").SemiBold().FontSize(8).FontColor(Colors.White);
                                h.Cell().Background(ColorSecondary).Padding(4).Text("Approved By").SemiBold().FontSize(8).FontColor(Colors.White);
                                h.Cell().Background(ColorSecondary).Padding(4).Text("Status").SemiBold().FontSize(8).FontColor(Colors.White);
                                h.Cell().Background(ColorSecondary).Padding(4).Text("Comments").SemiBold().FontSize(8).FontColor(Colors.White);
                            });

                            foreach (var vo in model.VariationOrders)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(vo.Date.ToString("yyyy/MM/dd")).FontSize(7.5f);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(vo.Description).FontSize(7.5f);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(vo.ApprovedBy ?? "-").FontSize(7.5f);
                                
                                var statusCell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4);
                                var isApproved = vo.Status?.Equals("Approved", StringComparison.OrdinalIgnoreCase) == true;
                                statusCell.Background(isApproved ? Colors.Green.Lighten5 : Colors.Grey.Lighten4)
                                    .Text(vo.Status ?? "-").FontSize(7.5f).FontColor(isApproved ? Colors.Green.Darken3 : Colors.Grey.Darken2).Bold();

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(vo.AdditionalComments ?? "-").FontSize(7.5f);
                            }
                        });
                    }
                });

                // Photos Grid
                if (model.IncidentPhotoPaths.Any())
                {
                    col.Item().PaddingTop(12).Column(photoCol =>
                    {
                        photoCol.Item().Text("INCIDENT & PROGRESS PHOTOS").FontSize(9).ExtraBold().FontColor(ColorSecondary);
                        photoCol.Item().PaddingTop(3).Grid(grid =>
                        {
                            grid.Columns(4); // 4 columns
                            grid.Spacing(8);

                            foreach (var path in model.IncidentPhotoPaths)
                            {
                                if (File.Exists(path))
                                {
                                    try
                                    {
                                        grid.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(2).Column(imgCol =>
                                        {
                                            imgCol.Item().Height(70).Image(path).FitArea();
                                            imgCol.Item().AlignCenter().Text(Path.GetFileName(path)).FontSize(6).FontColor(Colors.Grey.Darken1);
                                        });
                                    }
                                    catch
                                    {
                                        // Skip corrupted/invalid image files
                                    }
                                }
                            }
                        });
                    });
                }
            });
        }

        private void ComposeMetricBlock(IContainer container, string title, Action<ColumnDescriptor> content)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
            {
                col.Item().Background("#FBC02D").PaddingVertical(3).PaddingHorizontal(6).Text(title).FontSize(8).ExtraBold().FontColor(ColorSecondary);
                col.Item().Padding(5).Column(content);
            });
        }

        private string GetStatusColor(string status)
        {
            if (string.IsNullOrEmpty(status)) return "#374151";
            if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase)) return "#2E7D32"; 
            if (status.Equals("In Progress", StringComparison.OrdinalIgnoreCase) || status.Equals("Active", StringComparison.OrdinalIgnoreCase)) return "#1565C0"; 
            if (status.Equals("Delayed", StringComparison.OrdinalIgnoreCase)) return "#E64A19"; 
            return "#374151"; 
        }

        private string FormatDate(DateTime? dt)
        {
            return dt.HasValue ? dt.Value.ToString("yyyy/MM/dd") : "-";
        }

        private void ComposeProjectReportFooter(IContainer container, CompanyDetails company)
        {
            container.PaddingTop(8).BorderTop(1).BorderColor(Colors.Grey.Lighten2).Row(row =>
            {
                row.RelativeItem().Text(x =>
                {
                    x.Span("Page ").FontSize(8);
                    x.CurrentPageNumber().FontSize(8);
                    x.Span(" of ").FontSize(8);
                    x.TotalPages().FontSize(8);
                });

                row.RelativeItem().AlignRight().Text($"Orange Circle Construction © {DateTime.Now.Year} - Confidential Daily Project Status Report").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }
    }
}
