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

        private object? GetRawPropertyValue(object item, string propertyName)
        {
            if (item == null) return null;
            var prop = item.GetType().GetProperty(propertyName);
            return prop?.GetValue(item);
        }

        private string GetPropertyValue(object item, string propertyName)
        {
            return FormatValue(GetRawPropertyValue(item, propertyName));
        }
    }
}
