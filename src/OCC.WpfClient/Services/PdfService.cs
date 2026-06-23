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

        // ─── Leave Form PDF ───────────────────────────────────────────────────────

        public async Task<string> GenerateLeaveFormPdfAsync(LeaveRequest request)
        {
            var company = new CompanyDetails();
            var employee = request.Employee;
            var empName   = employee != null ? $"{employee.FirstName} {employee.LastName}" : "Unknown";
            var empNo     = employee?.EmployeeNumber ?? "—";
            var empBranch = employee?.Branch ?? "—";
            var empRole   = employee?.Role.ToString() ?? "—";

            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeLeaveHeader(c, company));

                        page.Content().PaddingVertical(16).Column(col =>
                        {
                            // ── Employee Details ──
                            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Column(box =>
                            {
                                box.Item().Background(ColorPrimary).Padding(8)
                                   .Text("EMPLOYEE DETAILS").SemiBold().FontColor(Colors.White).FontSize(10);
                                box.Item().Padding(12).Row(row =>
                                {
                                    row.RelativeItem().Column(c2 =>
                                    {
                                        c2.Item().Text(t => { t.Span("Full Name:  ").SemiBold(); t.Span(empName); });
                                        c2.Item().PaddingTop(4).Text(t => { t.Span("Employee No: ").SemiBold(); t.Span(empNo); });
                                        c2.Item().PaddingTop(4).Text(t => { t.Span("Role / Trade:  ").SemiBold(); t.Span(empRole); });
                                    });
                                    row.RelativeItem().Column(c2 =>
                                    {
                                        c2.Item().Text(t => { t.Span("Branch:  ").SemiBold(); t.Span(empBranch); });
                                        c2.Item().PaddingTop(4).Text(t => { t.Span("Date Submitted: ").SemiBold(); t.Span(request.CreatedDate.ToString("dd MMM yyyy")); });
                                    });
                                });
                            });

                            col.Item().Height(14);

                            // ── Leave Details Table ──
                            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Column(box =>
                            {
                                box.Item().Background(ColorSecondary).Padding(8)
                                   .Text("LEAVE DETAILS").SemiBold().FontColor(Colors.White).FontSize(10);
                                box.Item().Padding(12).Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(); cols.RelativeColumn();
                                        cols.RelativeColumn(); cols.RelativeColumn();
                                    });
                                    table.Header(h =>
                                    {
                                        foreach (var hdr in new[] { "LEAVE TYPE", "FROM", "TO", "WORKING DAYS" })
                                            h.Cell().Background(ColorLightOrange).BorderBottom(1)
                                             .BorderColor(Colors.Grey.Lighten2).Padding(6)
                                             .Text(hdr).SemiBold().FontSize(9).FontColor(ColorSecondary);
                                    });
                                    table.Cell().Padding(6).Text(request.LeaveType.ToString());
                                    table.Cell().Padding(6).Text(request.StartDate.ToString("dd MMM yyyy"));
                                    table.Cell().Padding(6).Text(request.EndDate.ToString("dd MMM yyyy"));
                                    table.Cell().Padding(6).Text(request.NumberOfDays.ToString()).Bold().FontColor(ColorPrimary);
                                });
                                box.Item().Padding(12).Row(row =>
                                {
                                    row.RelativeItem().Text(t => { t.Span("Paid Leave: ").SemiBold(); t.Span(request.IsUnpaid ? "No (Unpaid)" : "Yes"); });
                                    row.RelativeItem().Text(t =>
                                    {
                                        t.Span("Status: ").SemiBold();
                                        t.Span(request.Status.ToString()).FontColor(
                                            request.Status == LeaveStatus.Approved ? "#4CAF50" :
                                            request.Status == LeaveStatus.Rejected ? "#F44336" : ColorPrimary);
                                    });
                                });
                            });

                            col.Item().Height(14);

                            // ── Reason ──
                            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Column(box =>
                            {
                                box.Item().Background(ColorLightOrange).Padding(8)
                                   .Text("REASON FOR LEAVE").SemiBold().FontSize(10).FontColor(ColorSecondary);
                                box.Item().MinHeight(70).Padding(12)
                                   .Text(string.IsNullOrWhiteSpace(request.Reason) ? "(No reason provided)" : request.Reason)
                                   .FontColor(Colors.Grey.Darken3);
                            });

                            if (!string.IsNullOrWhiteSpace(request.AdminComment))
                            {
                                col.Item().Height(10);
                                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Column(box =>
                                {
                                    box.Item().Background(Colors.Grey.Lighten4).Padding(8)
                                       .Text("MANAGEMENT COMMENT").SemiBold().FontSize(10).FontColor(ColorSecondary);
                                    box.Item().Padding(12).Text(request.AdminComment).Italic();
                                });
                            }

                            col.Item().Height(30);

                            // ── Signature Blocks ──
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(sig =>
                                {
                                    sig.Item().Text("EMPLOYEE DECLARATION").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                                    sig.Item().PaddingTop(3).Text("I confirm the details above are correct and I am applying for leave as stated.")
                                       .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                                    sig.Item().PaddingTop(40).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                                    sig.Item().PaddingTop(4).Row(r =>
                                    {
                                        r.RelativeItem().Text("Signature").FontSize(8).FontColor(Colors.Grey.Medium);
                                        r.RelativeItem().AlignRight().Text("Date: ___/___/______").FontSize(8).FontColor(Colors.Grey.Medium);
                                    });
                                    sig.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                                    sig.Item().PaddingTop(4).Text("Print Name").FontSize(8).FontColor(Colors.Grey.Medium);
                                });

                                row.ConstantItem(40);

                                row.RelativeItem().Column(sig =>
                                {
                                    sig.Item().Text("MANAGER / HR APPROVAL").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                                    sig.Item().PaddingTop(3).Text($"Approved  /  Rejected  (circle)  —  Ref: {request.Id.ToString()[..8].ToUpper()}")
                                       .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                                    sig.Item().PaddingTop(40).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                                    sig.Item().PaddingTop(4).Row(r =>
                                    {
                                        r.RelativeItem().Text("Signature").FontSize(8).FontColor(Colors.Grey.Medium);
                                        r.RelativeItem().AlignRight().Text("Date: ___/___/______").FontSize(8).FontColor(Colors.Grey.Medium);
                                    });
                                    sig.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                                    sig.Item().PaddingTop(4).Text("Print Name").FontSize(8).FontColor(Colors.Grey.Medium);
                                });
                            });
                        });

                        page.Footer().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text("Orange Circle Construction — Leave Application Form — Human Resources Confidential")
                               .FontSize(7).FontColor(Colors.Grey.Medium);
                            row.RelativeItem().AlignRight()
                               .Text($"Ref: {request.Id.ToString()[..8].ToUpper()} — {DateTime.Now:dd MMM yyyy}")
                               .FontSize(7).FontColor(Colors.Grey.Medium);
                        });
                    });
                });

                var safeEmpName = empName.Replace(",", "").Replace(" ", "_");
                var fileName = $"LeaveForm_{safeEmpName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
                doc.GeneratePdf(filePath);
                return filePath;
            });
        }

        private void ComposeLeaveHeader(IContainer container, CompanyDetails company)
        {
            container.PaddingBottom(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(company.CompanyName).FontSize(22).ExtraBold().FontColor(ColorPrimary);
                    col.Item().Text("LEAVE APPLICATION").FontSize(14).SemiBold().FontColor(ColorSecondary);
                    col.Item().PaddingTop(2).Text("Human Resources — Confidential").FontSize(8).FontColor(Colors.Grey.Medium);
                });
                row.RelativeItem().AlignRight().Column(c =>
                {
                    var logoBytes = GetLogoBytes();
                    if (logoBytes != null)
                        c.Item().Height(60).AlignRight().Image(logoBytes).FitArea();
                });
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        //  WAGE RUN PDF — ported from OCC.Client PdfService_WageRun.cs
        // ═══════════════════════════════════════════════════════════════════

        public async Task<string> GenerateWageRunPdfAsync(WageRun wageRun)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(10);
                        page.Size(PageSizes.A4.Landscape());
                        page.DefaultTextStyle(x => x.FontSize(6.5f).FontFamily(Fonts.Arial).FontColor(Colors.Black));

                        page.Header().Element(c => ComposeWageHeader(c, wageRun));
                        page.Content().PaddingVertical(5).Element(c => ComposeWageContent(c, wageRun));
                        page.Footer().PaddingTop(5).Element(c => ComposeWageRunFooter(c));
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

        private void ComposeWageHeader(IContainer container, WageRun wageRun)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("ORANGE CIRCLE CONSTRUCTION Ltd").FontSize(10).ExtraBold();
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

                // Summary tables at the bottom
                col.Item().PaddingTop(20).Row(row =>
                {
                    row.ConstantItem(150).Element(c => ComposeLoanSummary(c));
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
                    columns.ConstantColumn(12);   // #
                    columns.ConstantColumn(22);   // BAS
                    columns.RelativeColumn(2.2f); // NAME
                    columns.ConstantColumn(28);   // RATE P/HR
                    columns.ConstantColumn(20);   // HRS
                    columns.ConstantColumn(28);   // STD O/T RATE
                    columns.ConstantColumn(28);   // SAT O/T RATE
                    columns.ConstantColumn(28);   // SUN P/H RATE
                    columns.ConstantColumn(20);   // STD O/T HRS
                    columns.ConstantColumn(20);   // SAT O/T HRS
                    columns.ConstantColumn(20);   // SUN O/T HRS
                    columns.ConstantColumn(28);   // LOANS
                    columns.ConstantColumn(28);   // WASHING
                    columns.ConstantColumn(28);   // GAS
                    columns.ConstantColumn(28);   // OTHER
                    columns.ConstantColumn(35);   // TOTAL NETT
                    columns.ConstantColumn(35);   // BANK
                    columns.RelativeColumn(1.8f); // COMMENTS
                    columns.ConstantColumn(35);   // TOTAL REM
                    columns.ConstantColumn(30);   // RATE P/DAY
                    columns.ConstantColumn(20);   // W1
                    columns.ConstantColumn(20);   // W2
                    columns.ConstantColumn(20);   // TOT D
                    columns.ConstantColumn(22);   // H/D
                });

                table.Header(header =>
                {
                    header.Cell().Element(WageHeaderStyle).Text("#");
                    header.Cell().Element(WageHeaderStyle).Text("BAS");
                    header.Cell().Element(WageHeaderStyle).Text("NAME");
                    header.Cell().Element(WageHeaderStyle).Text("RATE\nP/HR");
                    header.Cell().Element(WageHeaderStyle).Text("HRS");
                    header.Cell().Element(WageHeaderStyle).Text("STD O/T\nRATE");
                    header.Cell().Element(WageHeaderStyle).Text("SAT O/T\nRATE");
                    header.Cell().Element(WageHeaderStyle).Text("SUN P/H\nRATE");
                    header.Cell().Element(WageHeaderStyle).Text("STD\nO/T");
                    header.Cell().Element(WageHeaderStyle).Text("SAT\nO/T");
                    header.Cell().Element(WageHeaderStyle).Text("SUN\nO/T");
                    header.Cell().Element(WageHeaderStyle).Text("LOANS");
                    header.Cell().Element(WageHeaderStyle).Text("WASH-\nING");
                    header.Cell().Element(WageHeaderStyle).Text("GAS");
                    header.Cell().Element(WageHeaderStyle).Text("OTHER");
                    header.Cell().Element(WageHeaderStyle).Text("TOTAL\nNETT");
                    header.Cell().Element(WageHeaderStyle).Text("BANK");
                    header.Cell().Element(WageHeaderStyle).Text("COMMENTS");
                    header.Cell().Element(WageHeaderStyle).Text("TOTAL\nREM");
                    header.Cell().Element(WageHeaderStyle).Text("RATE\nP/DAY");
                    header.Cell().Element(WageHeaderStyle).Text("W1");
                    header.Cell().Element(WageHeaderStyle).Text("W2");
                    header.Cell().Element(WageHeaderStyle).Text("TOT\nD");
                    header.Cell().Element(WageHeaderStyle).Text("H/D");

                    static IContainer WageHeaderStyle(IContainer c) =>
                        c.Border(0.5f).Background(Colors.Grey.Lighten4).Padding(1)
                         .AlignCenter().AlignMiddle()
                         .DefaultTextStyle(x => x.Bold().FontSize(5.5f));
                });

                int index = 1;
                foreach (var line in lines)
                {
                    table.Cell().Element(WageCellStyle).Text(index++.ToString());
                    table.Cell().Element(WageCellStyle).Text(line.EmployeeNumber ?? "");
                    table.Cell().Element(WageCellStyle).Text(line.EmployeeName ?? "");
                    table.Cell().Element(WageCellStyle).AlignRight().Text(line.HourlyRate.ToString("F2"));

                    // Standard Hours = Normal + Projected + Variance
                    decimal stdHours = (decimal)(line.NormalHours + line.ProjectedHours + line.VarianceHours);
                    table.Cell().Element(WageCellStyle).AlignCenter().Text(stdHours.ToString("F2"));

                    table.Cell().Element(WageCellStyle).AlignRight().Text((line.HourlyRate * 1.5m).ToString("F2"));
                    table.Cell().Element(WageCellStyle).AlignRight().Text((line.HourlyRate * 1.5m).ToString("F2"));
                    table.Cell().Element(WageCellStyle).AlignRight().Text((line.HourlyRate * 2.0m).ToString("F2"));

                    table.Cell().Element(WageCellStyle).AlignCenter().Text(line.Overtime15Hours.ToString("F2"));
                    table.Cell().Element(WageCellStyle).AlignCenter().Text("0.00"); // SAT O/T (Sat = OT15, not separate)
                    table.Cell().Element(WageCellStyle).AlignCenter().Text(line.Overtime20Hours.ToString("F2"));

                    table.Cell().Element(WageCellStyle).AlignRight().Text(line.DeductionLoan.ToString("F2"));
                    table.Cell().Element(WageCellStyle).AlignRight().Text(line.DeductionWashing.ToString("F2"));
                    table.Cell().Element(WageCellStyle).AlignRight().Text(line.DeductionGas.ToString("F2"));

                    // OTHER = PPE + Other
                    decimal otherTotal = line.DeductionOther + line.DeductionPPE;
                    table.Cell().Element(WageCellStyle).AlignRight().Text(otherTotal.ToString("F2"));

                    table.Cell().Element(WageCellStyle).AlignRight().Text(line.NetPay.ToString("F2")).SemiBold();
                    table.Cell().Element(WageCellStyle).Text(line.BankName ?? "");

                    var comments = line.VarianceNotes ?? "";
                    if (line.IncentiveSupervisor > 0) comments = "SUPERVISOR FEE " + comments;
                    table.Cell().Element(WageCellStyle).Text(comments.Trim());

                    // Total Rem = NetPay + IncentiveSupervisor
                    decimal totalRem = line.NetPay + line.IncentiveSupervisor;
                    table.Cell().Element(WageCellStyle).AlignRight().Text(totalRem.ToString("F2"));

                    // Rate per day = HourlyRate × 8.75
                    table.Cell().Element(WageCellStyle).AlignRight().Text((line.HourlyRate * 8.75m).ToString("F2"));
                    table.Cell().Element(WageCellStyle).AlignCenter().Text(line.DaysWorkedWeek1.ToString("0"));
                    table.Cell().Element(WageCellStyle).AlignCenter().Text(line.DaysWorkedWeek2.ToString("0"));
                    table.Cell().Element(WageCellStyle).AlignCenter().Text(line.TotalDaysWorked.ToString("0"));

                    double totalHrs = line.NormalHours + line.Overtime15Hours + line.Overtime20Hours + line.ProjectedHours;
                    double hpd = line.TotalDaysWorked > 0 ? totalHrs / line.TotalDaysWorked : 0;
                    table.Cell().Element(WageCellStyle).AlignCenter().Text(hpd.ToString("F1"));

                    static IContainer WageCellStyle(IContainer c) =>
                        c.Border(0.5f).Padding(1).AlignMiddle();
                }

                // Footer totals row
                table.Footer(footer =>
                {
                    footer.Cell().ColumnSpan(15).Element(c => c.AlignRight().PaddingRight(5).Text("TOTAL:").Bold());
                    footer.Cell().Element(c => c.Border(0.5f).Padding(1).AlignRight()
                        .Text(lines.Sum(x => x.NetPay).ToString("F2")).Bold());
                    footer.Cell().ColumnSpan(8).Element(c => c.Border(0.5f));
                });
            });
        }

        private void ComposeLoanSummary(IContainer container)
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
                    header.Cell().Element(WageTotStyle).Text("LOANS");
                    header.Cell().Element(WageTotStyle).Text("WASHING");
                    header.Cell().Element(WageTotStyle).Text("GAS");
                    header.Cell().Element(WageTotStyle).Text("LIVING OUT");
                    header.Cell().Element(WageTotStyle).Text("TOTAL");

                    static IContainer WageTotStyle(IContainer c) =>
                        c.Border(0.5f).AlignCenter().DefaultTextStyle(x => x.Bold());
                });

                var permLines   = wageRun.Lines.Where(l => l.EmploymentType == "Permanent").ToList();
                var casualLines = wageRun.Lines.Where(l => l.EmploymentType != "Permanent").ToList();

                AddWageTotalRow(table, "Permanent Staff", permLines);
                AddWageTotalRow(table, "Casual Staff",    casualLines);

                // Grand Total
                table.Cell().Element(WageTotLineStyle).Text("Total").Bold();
                table.Cell().Element(WageTotLineStyle).AlignRight().Text(wageRun.Lines.Sum(x => x.DeductionLoan).ToString("F2")).Bold();
                table.Cell().Element(WageTotLineStyle).AlignRight().Text(wageRun.Lines.Sum(x => x.DeductionWashing).ToString("F2")).Bold();
                table.Cell().Element(WageTotLineStyle).AlignRight().Text(wageRun.Lines.Sum(x => x.DeductionGas).ToString("F2")).Bold();
                table.Cell().Element(WageTotLineStyle).AlignRight().Text("0.00").Bold();
                table.Cell().Element(WageTotLineStyle).Background(Colors.Grey.Lighten3).AlignRight()
                    .Text(wageRun.Lines.Sum(x => x.NetPay).ToString("F2")).Bold();

                static void AddWageTotalRow(TableDescriptor t, string label, List<WageRunLine> ls)
                {
                    t.Cell().Element(WageTotLineStyle).Text(label);
                    t.Cell().Element(WageTotLineStyle).AlignRight().Text(ls.Sum(x => x.DeductionLoan).ToString("F2"));
                    t.Cell().Element(WageTotLineStyle).AlignRight().Text(ls.Sum(x => x.DeductionWashing).ToString("F2"));
                    t.Cell().Element(WageTotLineStyle).AlignRight().Text(ls.Sum(x => x.DeductionGas).ToString("F2"));
                    t.Cell().Element(WageTotLineStyle).AlignRight().Text("0.00");
                    t.Cell().Element(WageTotLineStyle).AlignRight().Text(ls.Sum(x => x.NetPay).ToString("F2"));
                }

                static IContainer WageTotLineStyle(IContainer c) =>
                    c.Border(0.5f).PaddingHorizontal(2).AlignMiddle();
            });
        }

        private void ComposeWageRunFooter(IContainer container)
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
                row.RelativeItem().AlignRight().Text($"Generated on {DateTime.Now:F} - Orange Circle Construction");
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        //  LOAN SCHEDULE PDF — ported from OCC.Client PdfService_Loan.cs
        // ═══════════════════════════════════════════════════════════════════

        public async Task<string> GenerateLoanSchedulePdfAsync(EmployeeLoan loan, Employee employee)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeLoanHeader(c, employee, loan));
                        page.Content().PaddingVertical(20).Element(c => ComposeLoanContent(c, employee, loan));
                        page.Footer().Element(c => ComposeLoanFooter(c));
                    });
                });

                string tempPath = Path.GetTempPath();
                string filename = $"Loan_{employee.LastName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string fullPath = Path.Combine(tempPath, filename);
                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        private void ComposeLoanHeader(IContainer container, Employee employee, EmployeeLoan loan)
        {
            container.Row(row =>
            {
                row.RelativeItem(3).Column(col =>
                {
                    col.Item().Text("Orange Circle Construction").FontSize(22).ExtraBold().FontColor(ColorPrimary);
                    col.Item().Text("Employee Loan Agreement").FontSize(12).FontColor(Colors.Grey.Medium);
                });
                row.RelativeItem(2).AlignRight().Column(col =>
                {
                    col.Item().Text($"Date: {loan.StartDate:dd MMM yyyy}").FontSize(14).SemiBold().FontColor(ColorSecondary);
                    col.Item().Text($"Ref: {employee.EmployeeNumber ?? "N/A"}").FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }

        private void ComposeLoanContent(IContainer container, Employee employee, EmployeeLoan loan)
        {
            container.Column(col =>
            {
                // Employee info block
                col.Item().Background(Colors.Grey.Lighten5).Padding(15).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(employee.DisplayName).FontSize(16).Bold().FontColor(ColorSecondary);
                        c.Item().Text(employee.IdNumber ?? "").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text(employee.Branch ?? "No Branch").FontSize(12).SemiBold().FontColor(Colors.Grey.Darken2);
                    });
                });

                // Loan details grid
                col.Item().PaddingTop(20).Element(c => ComposeLoanDetails(c, loan));

                // Terms
                col.Item().PaddingTop(30).Text("Terms and Conditions").FontSize(12).Bold().Underline();
                col.Item().PaddingTop(10).Text("1. The employee acknowledges the debt and agrees to repay the loan in the installments specified above.");
                col.Item().Text("2. Installments will be deducted directly from the employee's salary/wages.");
                col.Item().Text("3. Interest is calculated as specified. Early repayment is permitted without penalty.");
                col.Item().Text("4. If employment is terminated, the outstanding balance becomes immediately due and payable.");

                // Signatures
                col.Item().PaddingTop(50).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        c.Item().PaddingTop(5).Text("Employee Signature").FontSize(10);
                    });
                    row.ConstantItem(50);
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        c.Item().PaddingTop(5).Text("Employer Signature").FontSize(10);
                    });
                });
            });
        }

        public static (string Frequency, int Installments, string ActualNotes) ParseLoanNotes(string notes)
        {
            if (string.IsNullOrEmpty(notes))
                return ("Monthly", 0, string.Empty);

            if (notes.StartsWith("[Term:") && notes.Contains("Installments:"))
            {
                try
                {
                    int termEnd = notes.IndexOf(']');
                    if (termEnd > 0)
                    {
                        string header = notes.Substring(1, termEnd - 1); // "Term: Fortnightly, Installments: 10"
                        string actualNotes = notes.Substring(termEnd + 1).Trim();
                        
                        string[] parts = header.Split(',');
                        string freq = "Monthly";
                        int inst = 0;
                        foreach (var part in parts)
                        {
                            if (part.Contains("Term:"))
                                freq = part.Replace("Term:", "").Trim();
                            else if (part.Contains("Installments:"))
                                int.TryParse(part.Replace("Installments:", "").Trim(), out inst);
                        }
                        return (freq, inst, actualNotes);
                    }
                }
                catch { }
            }
            return ("Monthly", 0, notes);
        }

        private void ComposeLoanDetails(IContainer container, EmployeeLoan loan)
        {
            var (frequency, installments, actualNotes) = ParseLoanNotes(loan.Notes);

            container.Background(Colors.Grey.Lighten5).Border(1).BorderColor(Colors.Grey.Lighten3).Padding(15).Column(col =>
            {
                col.Item().PaddingBottom(10).Text("Loan Details").FontSize(12).SemiBold();

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("Principal Amount:").SemiBold();
                    row.RelativeItem().AlignRight().Text($"{loan.PrincipalAmount:C}");
                });
                col.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Interest Rate:").SemiBold();
                    row.RelativeItem().AlignRight().Text($"{loan.InterestRate}%");
                });
                col.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Repayment Frequency:").SemiBold();
                    row.RelativeItem().AlignRight().Text(frequency);
                });
                if (installments > 0)
                {
                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("Repayment Terms:").SemiBold();
                        row.RelativeItem().AlignRight().Text($"{installments} Installments");
                    });
                }
                col.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Installment Amount:").SemiBold();
                    row.RelativeItem().AlignRight().Text($"{loan.MonthlyInstallment:C}");
                });

                if (!string.IsNullOrEmpty(actualNotes))
                {
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(5).Text($"Notes: {actualNotes}").FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
                }

                col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                decimal totalRepayable = CalculateLoanTotalRepayable(loan.PrincipalAmount, loan.MonthlyInstallment, loan.InterestRate);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("ESTIMATED TOTAL REPAYABLE:").Bold();
                    row.RelativeItem().AlignRight().Text($"{totalRepayable:C}").Bold();
                });
            });
        }

        private void ComposeLoanFooter(IContainer container)
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
                row.RelativeItem().AlignRight().Text($"Generated on {DateTime.Now:F} - Orange Circle Construction");
            });
        }

        /// <summary>
        /// Flat simple interest total repayable: Total = P + (P * rate / 100)
        /// </summary>
        private decimal CalculateLoanTotalRepayable(decimal principal, decimal installment, decimal rate)
        {
            if (principal <= 0) return 0;
            return principal + (principal * rate / 100);
        }

        public async Task<string> GenerateLoanStatementPdfAsync(OCC.Shared.DTOs.LoanStatementDto statement)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeStatementHeader(c, statement));
                        page.Content().PaddingVertical(20).Element(c => ComposeStatementContent(c, statement));
                        page.Footer().Element(c => ComposeStatementFooter(c));
                    });
                });

                string tempPath = Path.GetTempPath();
                string filename = $"LoanStatement_{statement.EmployeeName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string fullPath = Path.Combine(tempPath, filename);
                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        private void ComposeStatementHeader(IContainer container, OCC.Shared.DTOs.LoanStatementDto statement)
        {
            container.Row(row =>
            {
                row.RelativeItem(3).Column(col =>
                {
                    col.Item().Text("Orange Circle Construction").FontSize(22).ExtraBold().FontColor(ColorPrimary);
                    col.Item().Text("Employee Loan Statement").FontSize(12).FontColor(Colors.Grey.Medium);
                });
                row.RelativeItem(2).AlignRight().Column(col =>
                {
                    col.Item().Text($"Date: {DateTime.Today:dd MMM yyyy}").FontSize(14).SemiBold().FontColor(ColorSecondary);
                    col.Item().Text($"Ref: {statement.EmployeeNumber}").FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }

        private void ComposeStatementContent(IContainer container, OCC.Shared.DTOs.LoanStatementDto statement)
        {
            container.Column(col =>
            {
                // Employee details block
                col.Item().Background(Colors.Grey.Lighten5).Padding(15).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(statement.EmployeeName).FontSize(16).Bold().FontColor(ColorSecondary);
                        c.Item().Text($"Loan Start Date: {statement.StartDate:dd MMM yyyy}").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text("Statement Account Summary").FontSize(12).SemiBold().FontColor(Colors.Grey.Darken2);
                    });
                });

                // Loan Account Overview
                col.Item().PaddingTop(15).Background(Colors.Grey.Lighten5).Border(1).BorderColor(Colors.Grey.Lighten3).Padding(15).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Principal Loan Amount:").SemiBold();
                        r.RelativeItem().AlignRight().Text($"{statement.PrincipalAmount:C}");
                    });
                    c.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text("Interest Rate:").SemiBold();
                        r.RelativeItem().AlignRight().Text($"{statement.InterestRate}%");
                    });
                    c.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text("Installment Amount:").SemiBold();
                        r.RelativeItem().AlignRight().Text($"{statement.MonthlyInstallment:C}");
                    });
                    c.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text("Current Outstanding Balance:").Bold().FontColor(ColorPrimary);
                        r.RelativeItem().AlignRight().Text($"{statement.OutstandingBalance:C}").Bold().FontColor(ColorPrimary);
                    });
                });

                // Transaction Table
                col.Item().PaddingTop(25).Text("Repayment Transaction History").FontSize(12).Bold();

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // Date
                        columns.RelativeColumn(4); // Description
                        columns.RelativeColumn(2); // Amount Paid
                        columns.RelativeColumn(2); // Remaining Balance
                    });

                    // Header row
                    table.Header(header =>
                    {
                        header.Cell().Background(ColorPrimary).Padding(8).Text("Payment Date").FontColor(Colors.White).Bold();
                        header.Cell().Background(ColorPrimary).Padding(8).Text("Description").FontColor(Colors.White).Bold();
                        header.Cell().Background(ColorPrimary).Padding(8).AlignRight().Text("Amount Paid").FontColor(Colors.White).Bold();
                        header.Cell().Background(ColorPrimary).Padding(8).AlignRight().Text("Balance").FontColor(Colors.White).Bold();
                    });

                    // Initial balance row
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Text($"{statement.StartDate:dd MMM yyyy}");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Text("Initial Principal Loan Advanced");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(8).AlignRight().Text("-");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(8).AlignRight().Text($"{statement.PrincipalAmount:C}");

                    // Payment rows
                    foreach (var p in statement.Payments)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Text($"{p.Date:dd MMM yyyy}");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Text(p.Notes);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(8).AlignRight().Text($"{p.Amount:C}");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(8).AlignRight().Text($"{p.BalanceAfterPayment:C}");
                    }
                });
            });
        }

        private void ComposeStatementFooter(IContainer container)
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
                row.RelativeItem().AlignRight().Text($"Generated on {DateTime.Now:F} - Orange Circle Construction");
            });
        }
    }
}
