using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using Microsoft.Extensions.Logging;
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

        private readonly IProjectService? _projectService;
        private readonly ILogger<PdfService>? _logger;

        public PdfService(IProjectService? projectService = null, ILogger<PdfService>? logger = null)
        {
            _projectService = projectService;
            _logger = logger;
            // Initializing QuestPDF with the Community License
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<string> GenerateOrderPdfAsync(Order order, bool isPrintVersion = false)
        {
            // Use hardcoded CompanyDetails for now to match legacy behavior
            var company = new CompanyDetails();

            Project? project = null;
            if (order.DestinationType == OrderDestinationType.Site && order.ProjectId.HasValue && _projectService != null)
            {
                try
                {
                    project = await _projectService.GetProjectAsync(order.ProjectId.Value);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load project details for PDF generation.");
                }
            }

            // Path to save the PDF
            var fileName = $"Order_{order.OrderNumber}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            await Task.Run(() =>
            {
                Document.Create(container =>
                {
                    ComposePremium(container, order, company, project, isPrintVersion);
                }).GeneratePdf(filePath);
            });

            return filePath;
        }

        #region Premium Design (Legacy)

        private void ComposePremium(IDocumentContainer container, Order order, CompanyDetails company, Project? project, bool isPrint)
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
                page.Content().PaddingHorizontal(20).PaddingVertical(20).Element(c => ComposePremiumContent(c, order, company, project, isPrint));
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

        private void ComposePremiumContent(IContainer container, Order order, CompanyDetails company, Project? project, bool isPrint)
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
                                if (order.DestinationType == OrderDestinationType.Site && project != null)
                                {
                                    details.Item().Text(project.Name).Bold();
                                    if (!string.IsNullOrEmpty(project.StreetLine1))
                                        details.Item().Text(project.StreetLine1);
                                    if (!string.IsNullOrEmpty(project.StreetLine2))
                                        details.Item().Text(project.StreetLine2);
                                    
                                    var cityPostal = "";
                                    if (!string.IsNullOrEmpty(project.City))
                                        cityPostal += project.City;
                                    if (!string.IsNullOrEmpty(project.PostalCode))
                                        cityPostal += (string.IsNullOrEmpty(cityPostal) ? "" : ", ") + project.PostalCode;
                                        
                                    if (!string.IsNullOrEmpty(cityPostal))
                                        details.Item().Text(cityPostal);
                                }
                                else if (order.DestinationType == OrderDestinationType.Other)
                                {
                                    if (!string.IsNullOrWhiteSpace(order.Notes))
                                    {
                                        var lines = order.Notes.Split(new[] { "\r\n", "\r", "\n", ", " }, StringSplitOptions.RemoveEmptyEntries);
                                        foreach (var line in lines)
                                        {
                                            details.Item().Text(line.Trim());
                                        }
                                    }
                                    else
                                    {
                                        details.Item().Text("Manual Delivery Address");
                                    }
                                }
                                else
                                {
                                    details.Item().Text(company.CompanyName).SemiBold();
                                    details.Item().Text(branchDetails.AddressLine1);
                                    if (!string.IsNullOrEmpty(branchDetails.AddressLine2))
                                        details.Item().Text(branchDetails.AddressLine2);
                                    details.Item().Text($"{branchDetails.City}, {branchDetails.PostalCode}");
                                }
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
             container.Table(table =>
             {
                 // Define Columns based on ViewModel properties we expect
                 // Date, In, Out, Status, Hours, Wage
                 table.ColumnsDefinition(columns =>
                 {
                     columns.RelativeColumn(); // Date
                     columns.ConstantColumn(60); // In
                     columns.ConstantColumn(60); // Out
                     columns.ConstantColumn(80); // Status
                     columns.ConstantColumn(60); // Hours
                     columns.ConstantColumn(80); // Wage
                 });
                 
                 table.Header(header =>
                 {
                     header.Cell().Element(HeaderStyle).Text("Date");
                     header.Cell().Element(HeaderStyle).Text("In");
                     header.Cell().Element(HeaderStyle).Text("Out");
                     header.Cell().Element(HeaderStyle).Text("Status");
                     header.Cell().Element(HeaderStyle).AlignRight().Text("Hours");
                     header.Cell().Element(HeaderStyle).AlignRight().Text("Wage");
                     
                     static IContainer HeaderStyle(IContainer container)
                     {
                         return container.Background(Colors.Grey.Lighten4).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).DefaultTextStyle(x => x.SemiBold());
                     }
                 });
                 
                 var props = typeof(T).GetProperties();

                 foreach(var item in data)
                 {
                     // Reflection to get values by order or name?
                     // Expected anonymous object: Date, In, Out, Status, Hours, Wage
                     // We trust the order from ViewModel: Date, In, Out, Status, Hours, Wage
                     
                     // Helper to safe get
                     string GetVal(string name) => props.FirstOrDefault(p => p.Name == name)?.GetValue(item)?.ToString() ?? "";
                     
                     table.Cell().Element(CellStyle).Text(GetVal("Date"));
                     table.Cell().Element(CellStyle).Text(GetVal("In"));
                     table.Cell().Element(CellStyle).Text(GetVal("Out"));
                     table.Cell().Element(CellStyle).Text(GetVal("Status"));
                     table.Cell().Element(CellStyle).AlignRight().Text(GetVal("Hours"));
                     table.Cell().Element(CellStyle).AlignRight().Text(GetVal("Wage"));
                     
                     static IContainer CellStyle(IContainer container)
                     {
                         return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten4).PaddingVertical(5);
                     }
                 }
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
        public async Task<string> GenerateEmployeeProfilePdfAsync(Employee employee)
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
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeGenericHeader(c, $"Employee Profile: {employee.DisplayName}", company));
                        page.Content().PaddingVertical(15).Element(c => ComposeEmployeeProfileContent(c, employee));
                        page.Footer().Element(c => ComposeGenericFooter(c, company));
                    });
                });

                string docsPath = Path.GetTempPath();
                string filename = $"EmployeeProfile_{employee.LastName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string fullPath = Path.Combine(docsPath, filename);

                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        private void ComposeEmployeeProfileContent(IContainer container, Employee employee)
        {
            container.Row(row =>
            {
                // Left Column
                row.RelativeItem().PaddingRight(10).Column(col =>
                {
                    col.Item().Element(c => ComposeCard(c, "Personal & Contact Details", innerCol =>
                    {
                        innerCol.Item().Element(sc => InfoRow(sc, "First Name", employee.FirstName));
                        innerCol.Item().Element(sc => InfoRow(sc, "Last Name", employee.LastName));
                        innerCol.Item().Element(sc => InfoRow(sc, "Employee Number", employee.EmployeeNumber));
                        innerCol.Item().Element(sc => InfoRow(sc, "ID Type", employee.IdType == IdType.RSAId ? "RSA ID" : "Passport"));
                        if (employee.IdType == IdType.RSAId)
                        {
                            innerCol.Item().Element(sc => InfoRow(sc, "ID Number", employee.IdNumber));
                        }
                        else
                        {
                            innerCol.Item().Element(sc => InfoRow(sc, "Passport Number", employee.IdNumber));
                            innerCol.Item().Element(sc => InfoRow(sc, "Permit Number", string.IsNullOrWhiteSpace(employee.PermitNumber) ? "-" : employee.PermitNumber));
                        }
                        innerCol.Item().Element(sc => InfoRow(sc, "Date of Birth", employee.DoB.ToString("yyyy-MM-dd")));
                        innerCol.Item().Element(sc => InfoRow(sc, "Tax Number", employee.TaxNumber ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Company Housing", employee.LivesInCompanyHousing ? "Yes" : "No"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Email", employee.Email ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Phone", employee.Phone ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Physical Address", employee.PhysicalAddress ?? "-"));
                    }));

                    col.Item().PaddingTop(15).Element(c => ComposeCard(c, "Emergency & Next of Kin", innerCol =>
                    {
                        innerCol.Item().Element(sc => InfoRow(sc, "Next of Kin Name", employee.NextOfKinName ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Relation", employee.NextOfKinRelation ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Kin Phone", employee.NextOfKinPhone ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Emergency Contact", employee.EmergencyContactName ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Contact Phone", employee.EmergencyContactPhone ?? "-"));
                    }));
                });

                // Right Column
                row.RelativeItem().PaddingLeft(10).Column(col =>
                {
                    col.Item().Element(c => ComposeCard(c, "Employment & Leave", innerCol =>
                    {
                        innerCol.Item().Element(sc => InfoRow(sc, "Role", employee.Role.ToString()));
                        innerCol.Item().Element(sc => InfoRow(sc, "Status", employee.Status.ToString()));
                        innerCol.Item().Element(sc => InfoRow(sc, "Employment Type", employee.EmploymentType.ToString()));
                        innerCol.Item().Element(sc => InfoRow(sc, "Contract Duration", employee.ContractDuration ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Branch", employee.Branch ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Employment Date", employee.EmploymentDate.ToString("yyyy-MM-dd")));
                        innerCol.Item().Element(sc => InfoRow(sc, "Shift Start", employee.ShiftStartTime?.ToString(@"hh\:mm") ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Shift End", employee.ShiftEndTime?.ToString(@"hh\:mm") ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Annual Leave Bal", $"{employee.AnnualLeaveBalance:F2} days"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Sick Leave Bal", $"{employee.SickLeaveBalance:F2} days"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Leave Cycle Start", employee.LeaveCycleStartDate?.ToString("yyyy-MM-dd") ?? "-"));
                    }));

                    col.Item().PaddingTop(15).Element(c => ComposeCard(c, "Financial Details", innerCol =>
                    {
                        innerCol.Item().Element(sc => InfoRow(sc, "Rate Type", employee.RateType.ToString()));
                        innerCol.Item().Element(sc => InfoRow(sc, "Hourly Rate", $"R {employee.HourlyRate:N2}"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Bank Name", employee.BankName ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Account Number", employee.AccountNumber ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Branch Code", employee.BranchCode ?? "-"));
                        innerCol.Item().Element(sc => InfoRow(sc, "Account Type", employee.AccountType ?? "-"));
                    }));
                });
            });

            static void ComposeCard(IContainer container, string title, Action<ColumnDescriptor> content)
            {
                container
                    .Border(1)
                    .BorderColor(Colors.Grey.Lighten3)
                    .Background(Colors.White)
                    .Padding(12)
                    .Column(col =>
                    {
                        col.Item().PaddingBottom(4).Text(title).FontSize(11).Bold().FontColor(ColorPrimary);
                        col.Item().BorderBottom(1).BorderColor(ColorPrimary).PaddingBottom(4);
                        content(col);
                    });
            }

            static void InfoRow(IContainer container, string label, string value)
            {
                container.BorderBottom(1).BorderColor(Colors.Grey.Lighten4).PaddingVertical(3).Row(row =>
                {
                    row.ConstantItem(100).Text(label).SemiBold().FontColor(Colors.Grey.Darken2);
                    row.RelativeItem().Text(value ?? "-");
                });
            }
        }

        public async Task<string> GenerateListReportPdfAsync<T>(string title, IEnumerable<T> items, List<ReportColumnDefinition> columns, bool isLandscape = false)
        {
            var company = new CompanyDetails();

            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        page.Size(isLandscape ? PageSizes.A4.Landscape() : PageSizes.A4);
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

        public async Task<string> GenerateGanttReportPdfAsync(string title, IEnumerable<GanttTaskPrintModel> items, DateTime minDate, DateTime maxDate)
        {
            var company = new CompanyDetails();
            var totalDays = (maxDate - minDate).TotalDays;
            if (totalDays <= 0) totalDays = 30;

            var interval = totalDays / 4.0;

            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        page.Size(PageSizes.A4.Landscape());
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeGenericHeader(c, title, company));
                        
                        page.Content().PaddingVertical(15).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(25);  // #
                                cols.RelativeColumn(3f); // Task Name
                                cols.ConstantColumn(60);  // Start
                                cols.ConstantColumn(60);  // Finish
                                cols.ConstantColumn(35);  // Prog
                                cols.RelativeColumn(4f); // Gantt Chart Area
                            });

                            table.Header(header =>
                            {
                                var headerBg = ColorPrimary;
                                
                                header.Cell().Background(headerBg).Padding(5).Text("#").SemiBold().FontColor(Colors.White).AlignCenter();
                                header.Cell().Background(headerBg).Padding(5).Text("Task Name").SemiBold().FontColor(Colors.White);
                                header.Cell().Background(headerBg).Padding(5).Text("Start").SemiBold().FontColor(Colors.White).AlignCenter();
                                header.Cell().Background(headerBg).Padding(5).Text("Finish").SemiBold().FontColor(Colors.White).AlignCenter();
                                header.Cell().Background(headerBg).Padding(5).Text("Prog").SemiBold().FontColor(Colors.White).AlignCenter();
                                
                                header.Cell().Background(headerBg).PaddingVertical(5).PaddingHorizontal(2).Row(row =>
                                {
                                    row.RelativeItem().Text(minDate.ToString("dd MMM")).FontSize(7).SemiBold().FontColor(Colors.White);
                                    row.RelativeItem().AlignRight().Text(minDate.AddDays(interval).ToString("dd MMM")).FontSize(7).SemiBold().FontColor(Colors.White);
                                    row.RelativeItem().AlignRight().Text(minDate.AddDays(interval * 2).ToString("dd MMM")).FontSize(7).SemiBold().FontColor(Colors.White);
                                    row.RelativeItem().AlignRight().Text(minDate.AddDays(interval * 3).ToString("dd MMM")).FontSize(7).SemiBold().FontColor(Colors.White);
                                    row.RelativeItem().AlignRight().Text(maxDate.ToString("dd MMM")).FontSize(7).SemiBold().FontColor(Colors.White);
                                });
                            });

                            foreach (var item in items)
                            {
                                var taskStart = item.StartDateRaw < minDate ? minDate : item.StartDateRaw;
                                var taskFinish = item.FinishDateRaw > maxDate ? maxDate : item.FinishDateRaw;
                                
                                var startOffset = (taskStart - minDate).TotalDays;
                                if (startOffset < 0) startOffset = 0;

                                var duration = (taskFinish - taskStart).TotalDays;
                                if (duration < 0.5) duration = 0.5;

                                var endOffset = totalDays - (startOffset + duration);
                                if (endOffset < 0) endOffset = 0;

                                var isOdd = item.Row % 2 != 0;
                                var rowBg = isOdd ? "#FAFAFA" : "#FFFFFF";

                                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(item.Row.ToString()).FontSize(8);
                                
                                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).PaddingLeft(4 + (float)item.IndentLevel * 10).Element(el => 
                                {
                                    var text = el.Text(item.TaskName.TrimStart()).FontSize(8);
                                    if (item.IsSummary)
                                    {
                                        text.Bold().FontColor(ColorPrimary);
                                    }
                                    else
                                    {
                                        text.FontColor(Colors.Black);
                                    }
                                });

                                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(item.StartDate).FontSize(8);
                                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(item.FinishDate).FontSize(8);
                                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(item.Progress).FontSize(8);

                                 table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4).PaddingHorizontal(2).AlignMiddle().Row(row =>
                                {
                                    if (startOffset > 0)
                                    {
                                        row.RelativeItem((float)startOffset);
                                    }

                                    var bar = row.RelativeItem((float)duration);
                                    if (item.IsSummary)
                                    {
                                        bar.Height(5).Background("#0D47A1").CornerRadius(1);
                                    }
                                    else
                                    {
                                        bar.Height(8).Background(Colors.Grey.Lighten3).Border(0.5f, Colors.Grey.Lighten1).CornerRadius(2).Element(barContainer =>
                                        {
                                            if (item.PercentComplete >= 100)
                                            {
                                                barContainer.Background("#1E80D6");
                                            }
                                            else if (item.PercentComplete > 0)
                                            {
                                                barContainer.Row(progRow =>
                                                {
                                                    progRow.RelativeItem((float)item.PercentComplete).Background("#1E80D6");
                                                    progRow.RelativeItem(100 - (float)item.PercentComplete);
                                                });
                                            }
                                        });
                                    }

                                    if (endOffset > 0)
                                    {
                                        row.RelativeItem((float)endOffset);
                                    }
                                });
                            }
                        });

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
                        page.Margin(20);
                        page.Size(PageSizes.A4.Landscape());
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeProjectReportHeader(c, model));
                        page.Content().PaddingTop(3).PaddingBottom(10).Element(c => ComposeProjectReportContent(c, model));
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
            container.Row(row =>
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

                // Main split: Left side (4/6 width) has cards 1-4 and dates/waste, Right side (2/6 width) has status summary
                col.Item().PaddingTop(12).Row(outerRow =>
                {
                    // Left Column (Cards 1-4 + Milestones & Waste)
                    outerRow.RelativeItem(4).Column(leftCol =>
                    {
                        // 4 Metric cards in a sub-row (so they share height with each other, but not with the summary)
                        leftCol.Item().Row(subRow =>
                        {
                            // Block 1: REPORT INFO
                            subRow.RelativeItem().PaddingRight(3).Element(c => ComposeMetricBlock(c, "REPORT INFO", blockCol =>
                            {
                                blockCol.Item().Text(t => { t.Span("Date: ").SemiBold(); t.Span(model.ReportDate.ToString("yyyy/MM/dd")); });
                                blockCol.Item().Text(t => { t.Span("Week: ").SemiBold(); t.Span(model.WeekNumber.ToString()); });
                                blockCol.Item().Text(t => { t.Span("Status: ").SemiBold(); t.Span(model.Project.Status).Bold().FontColor(GetStatusColor(model.Project.Status)); });
                            }));

                            // Block 2: POW REQUIREMENT
                            subRow.RelativeItem().PaddingHorizontal(1.5f).Element(c => ComposeMetricBlock(c, "POW PROGRESS", blockCol =>
                            {
                                blockCol.Item().Text(t => { t.Span("Required: ").SemiBold(); t.Span($"{model.PowPercentRequired:F1}%"); });
                                blockCol.Item().Text(t => { t.Span("Actual: ").SemiBold(); t.Span($"{model.OverallProgress:F1}%"); });
                                blockCol.Item().Text(t => { t.Span("Delay: ").SemiBold(); t.Span($"{model.DelayDays} Days").FontColor(model.DelayDays > 0 ? Colors.Red.Darken2 : ColorSecondary); });
                            }));

                            // Block 3: PROGRAM OF WORKS
                            subRow.RelativeItem().PaddingHorizontal(1.5f).Element(c => ComposeMetricBlock(c, "PROGRAM OF WORKS", blockCol =>
                            {
                                blockCol.Item().Text(t => { t.Span("Total Tasks: ").SemiBold(); t.Span(model.TotalTasks.ToString()); });
                                blockCol.Item().Text(t => { t.Span("Active: ").SemiBold(); t.Span(model.InProgressTasks.ToString()).FontColor(Colors.Blue.Darken2); });
                                blockCol.Item().Text(t => { t.Span("Completed: ").SemiBold(); t.Span(model.CompletedTasks.ToString()).FontColor(Colors.Green.Darken2); });
                            }));

                            // Block 4: SAFE WORKING HOURS
                            subRow.RelativeItem().PaddingLeft(3).Element(c => ComposeMetricBlock(c, "SAFE WORKING HOURS", blockCol =>
                            {
                                blockCol.Item().AlignCenter().Text($"{model.SafeWorkingHours:N0}").FontSize(14).ExtraBold().FontColor(Colors.Green.Darken3);
                                blockCol.Item().AlignCenter().Text("Safe Hours to Date").FontSize(7.5f).FontColor(Colors.Grey.Medium);
                            }));
                        });

                        // Milestones & Waste side-by-side (moved closer)
                        leftCol.Item().PaddingTop(14).Row(datesRow =>
                        {
                            // Left: Contract Dates & Milestones Card/Column
                            datesRow.RelativeItem(3).Column(datesCol =>
                            {
                                datesCol.Item().Text("CONTRACT DATES & MILESTONES").FontSize(9).ExtraBold().FontColor(ColorSecondary);
                                
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
                                datesCol.Item().PaddingTop(6).Text("OVERDUE MILESTONES").FontSize(8).ExtraBold().FontColor(ColorSecondary);
                                
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
                    });

                    // Right Column (Project Status Summary)
                    outerRow.RelativeItem(2).PaddingLeft(3).AlignTop().Element(c => ComposeMetricBlock(c, "PROJECT STATUS SUMMARY", blockCol =>
                    {
                        blockCol.Item().Text(model.StatusSummary).FontSize(8.5f).LineHeight(1.15f);
                    }));
                });

                // Row with Vendor Report (left) and Variation Orders (right) sharing the space
                col.Item().PaddingTop(16).Row(vendorVoRow =>
                {
                    // Left: Vendor Report
                    vendorVoRow.RelativeItem(3).Column(vendorCol =>
                    {
                        vendorCol.Item().Text("VENDOR REPORT - HSEQ COMPLIANCE").FontSize(9).ExtraBold().FontColor(ColorSecondary);
                        vendorCol.Item().PaddingTop(3).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2.2f);
                                cols.RelativeColumn(1.8f);
                                cols.RelativeColumn(1.6f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(1.2f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(ColorSecondary).Padding(4).Text("Vendor Name").SemiBold().FontSize(8).FontColor(Colors.White);
                                h.Cell().Background(ColorSecondary).Padding(4).Text("Scope").SemiBold().FontSize(8).FontColor(Colors.White);
                                h.Cell().Background(ColorSecondary).Padding(4).AlignCenter().Text("Safety File").SemiBold().FontSize(8).FontColor(Colors.White);
                                h.Cell().Background(ColorSecondary).Padding(4).AlignCenter().Text("AU 1").SemiBold().FontSize(8).FontColor(Colors.White);
                                h.Cell().Background(ColorSecondary).Padding(4).AlignCenter().Text("AU 2").SemiBold().FontSize(8).FontColor(Colors.White);
                                h.Cell().Background(ColorSecondary).Padding(4).AlignCenter().Text("AU 3").SemiBold().FontSize(8).FontColor(Colors.White);
                            });

                            foreach (var row in model.VendorReportRows.Where(r => r.VendorName.Equals("Orange Circle Construction", StringComparison.OrdinalIgnoreCase)))
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(row.VendorName).FontSize(7.5f);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(row.Scope).FontSize(7.5f);
                                
                                var safetyCell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter();
                                var isSafetyApproved = row.SafetyApproved?.Equals("Yes", StringComparison.OrdinalIgnoreCase) == true;
                                safetyCell.Text(row.SafetyApproved ?? "-").FontSize(7.5f).FontColor(isSafetyApproved ? Colors.Green.Darken2 : Colors.Red.Darken2).Bold();

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(row.Audit1 ?? "-").FontSize(7.5f);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(row.Audit2 ?? "-").FontSize(7.5f);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(row.Audit3 ?? "-").FontSize(7.5f);
                            }
                        });
                    });

                    vendorVoRow.ConstantItem(15);

                    // Right: Variation Orders
                    vendorVoRow.RelativeItem(3).Column(voCol =>
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
                });

                // Photos Grid (distributes 3 to 10 photos dynamically inside a single page grid layout)
                var validPhotos = model.IncidentPhotoPaths.Where(File.Exists).ToList();
                if (validPhotos.Any())
                {
                    col.Item().ShowEntire().PaddingTop(10).Column(photoCol =>
                    {
                        photoCol.Item().Text("REPORT & PROGRESS PHOTOS").FontSize(9).ExtraBold().FontColor(ColorSecondary);

                        var count = validPhotos.Count;
                        int colsCount = 4;
                        float imgHeight = 75;

                        if (count <= 4)
                        {
                            colsCount = count;
                            imgHeight = 110;
                        }
                        else if (count <= 8)
                        {
                            colsCount = 4;
                            imgHeight = 75;
                        }
                        else
                        {
                            colsCount = 5;
                            imgHeight = 70;
                        }

                        photoCol.Item().PaddingTop(4).Column(gridCol =>
                        {
                            for (int rowIndex = 0; rowIndex < (int)Math.Ceiling((double)count / colsCount); rowIndex++)
                            {
                                var rowPhotos = validPhotos.Skip(rowIndex * colsCount).Take(colsCount).ToList();
                                gridCol.Item().PaddingTop(rowIndex > 0 ? 5 : 0).Row(row =>
                                {
                                    foreach (var path in rowPhotos)
                                    {
                                        try
                                        {
                                            row.RelativeItem()
                                                .PaddingHorizontal(2.5f)
                                                .Border(0.4f)
                                                .BorderColor(Colors.Grey.Lighten2)
                                                .Height(imgHeight)
                                                .AlignCenter()
                                                .AlignMiddle()
                                                .Image(path)
                                                .FitArea();
                                        }
                                        catch
                                        {
                                            // Skip corrupted/invalid image files
                                        }
                                    }

                                    // If the last row is not full, add empty space relative columns to pad it out evenly!
                                    if (rowPhotos.Count < colsCount)
                                    {
                                        var emptyCount = colsCount - rowPhotos.Count;
                                        for (int ec = 0; ec < emptyCount; ec++)
                                        {
                                            row.RelativeItem().PaddingHorizontal(2.5f);
                                        }
                                    }
                                });
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
                                    bool isUnpaid = request.IsUnpaid || request.LeaveType == LeaveType.Unpaid || request.LeaveType == LeaveType.AbsentWithoutLeave;
                                    row.RelativeItem().Text(t => { t.Span("Paid Leave: ").SemiBold(); t.Span(isUnpaid ? "No (Unpaid)" : "Yes"); });
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

        public async Task<string> GenerateWageRunPdfAsync(WageRun wageRun, bool hideAfterComments = false, bool hideDecColumns = false, Dictionary<string, bool>? visibleColumns = null)
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
                        page.Content().PaddingVertical(5).Element(c => ComposeWageContent(c, wageRun, hideAfterComments, hideDecColumns, visibleColumns));
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

        private void ComposeWageHeader(IContainer container, WageRun wageRun)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("ORANGE CIRCLE CONSTRUCTION Ltd").FontSize(8).ExtraBold();
                    string headerTitle = "STAFF WAGES (OCC)";
                    if (string.Equals(wageRun.Branch, "Cape Town", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(wageRun.Branch, "CPT", StringComparison.OrdinalIgnoreCase))
                    {
                        headerTitle = "CAPE TOWN WAGE RUN";
                    }
                    else if (string.Equals(wageRun.Branch, "Johannesburg", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(wageRun.Branch, "JHB", StringComparison.OrdinalIgnoreCase))
                    {
                        headerTitle = "JOHANNESBURG WAGE RUN";
                    }
                    row.RelativeItem().AlignCenter().Text(headerTitle).FontSize(8).SemiBold();
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Date: ").SemiBold().FontSize(8);
                        t.Span(wageRun.EndDate.ToString("dd/MM/yyyy")).Underline().FontSize(8);
                    });
                });
            });
        }

        private void ComposeWageContent(IContainer container, WageRun wageRun, bool hideAfterComments, bool hideDecColumns, Dictionary<string, bool>? visibleColumns)
        {
            container.Column(col =>
            {
                var permLines = wageRun.Lines.Where(l => l.EmploymentType == "Permanent").OrderBy(l => l.EmployeeName).ToList();
                var casualLines = wageRun.Lines.Where(l => l.EmploymentType != "Permanent").OrderBy(l => l.EmployeeName).ToList();

                if (permLines.Any())
                {
                    col.Item().PaddingTop(5).Text("PERMANENT STAFF").FontSize(6).ExtraBold();
                    col.Item().Element(c => ComposeWageTable(c, permLines, hideAfterComments, hideDecColumns, visibleColumns, wageRun.Branch ?? ""));
                }

                if (casualLines.Any())
                {
                    col.Item().PaddingTop(10).Text("CONTRACT / CASUAL STAFF").FontSize(6).ExtraBold();
                    col.Item().Element(c => ComposeWageTable(c, casualLines, hideAfterComments, hideDecColumns, visibleColumns, wageRun.Branch ?? ""));
                }

                // Summary tables at the bottom
                col.Item().ShowEntire().PaddingTop(20).Row(row =>
                {
                    row.RelativeItem();
                    row.ConstantItem(380).Element(c => ComposeWageTotalsTable(c, wageRun));
                });
            });
        }

        private void ComposeWageTable(IContainer container, List<WageRunLine> lines, bool hideAfterComments, bool hideDecColumns, Dictionary<string, bool>? visibleColumns, string branch)
        {
            bool hasComments = lines.Any(l => !string.IsNullOrWhiteSpace(l.Comments));
            bool isCpt = branch.ToBranchEnum() == Branch.CPT;

            // Helper to check if a column is visible
            bool IsColVisible(string key)
            {
                if (hideAfterComments && (key == "TotalRem" || key == "Days")) return false;
                if (hideDecColumns && key == "DecColumns") return false;

                if (key == "Loans") return true;
                if (key == "Other") return true;
                if (key == "Washing" && isCpt) return false;
                if (key == "Gas" && isCpt) return false;
                if (key == "OccToBibc" && !isCpt) return false;

                bool standardDefault = key switch
                {
                    "OtRates" => false,
                    "DecColumns" => false,
                    "BankAccount" => false,
                    "Notes" => false,
                    "TotalRem" => false,
                    "Days" => false,
                    "OccToBibc" => isCpt,
                    "Washing" => !isCpt,
                    "Gas" => !isCpt,
                    "Loans" => true,
                    "Other" => true,
                    _ => true
                };

                if (visibleColumns == null) return standardDefault;
                return visibleColumns.TryGetValue(key, out bool isVisible) ? isVisible : standardDefault;
            }

            int visibleColCountBeforeNett = 0;
            if (IsColVisible("Index")) visibleColCountBeforeNett++;
            if (IsColVisible("Bas")) visibleColCountBeforeNett++;
            if (IsColVisible("Name")) visibleColCountBeforeNett++;
            if (IsColVisible("RateHr")) visibleColCountBeforeNett++;
            if (IsColVisible("Hrs")) visibleColCountBeforeNett++;
            if (IsColVisible("OtRates")) visibleColCountBeforeNett += 3;
            if (IsColVisible("DecColumns")) visibleColCountBeforeNett += 3;
            if (IsColVisible("OtHours")) visibleColCountBeforeNett += 3;
            if (IsColVisible("Loans")) visibleColCountBeforeNett++;
            if (IsColVisible("Washing")) visibleColCountBeforeNett++;
            if (IsColVisible("Gas")) visibleColCountBeforeNett++;
            if (IsColVisible("Other")) visibleColCountBeforeNett++;
            if (IsColVisible("OccToBibc")) visibleColCountBeforeNett++;
            
            int visibleColCountAfterNett = 0;
            if (IsColVisible("Bank")) visibleColCountAfterNett++;
            if (IsColVisible("BankAccount")) visibleColCountAfterNett++;
            if (IsColVisible("Comments")) visibleColCountAfterNett++;
            if (IsColVisible("Notes")) visibleColCountAfterNett++;
            if (IsColVisible("TotalRem")) visibleColCountAfterNett++;
            if (IsColVisible("Days")) visibleColCountAfterNett += 5;

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    if (IsColVisible("Index")) columns.RelativeColumn(0.4f);
                    if (IsColVisible("Bas")) columns.RelativeColumn(0.6f);
                    if (IsColVisible("Name")) columns.RelativeColumn(2.5f);
                    if (IsColVisible("RateHr")) columns.RelativeColumn(0.9f);
                    if (IsColVisible("Hrs")) columns.RelativeColumn(0.7f);
                    if (IsColVisible("OtRates"))
                    {
                        columns.RelativeColumn(0.9f); // STD O/T RATE
                        columns.RelativeColumn(0.9f); // SAT O/T RATE
                        columns.RelativeColumn(0.9f); // SUN P/H RATE
                    }
                    if (IsColVisible("DecColumns"))
                    {
                        columns.RelativeColumn(0.9f); // DEC O/T RATE
                        columns.RelativeColumn(0.7f); // DEC O/T HRS
                        columns.RelativeColumn(0.9f); // DEC TOTAL
                    }
                    if (IsColVisible("OtHours"))
                    {
                        columns.RelativeColumn(0.7f); // STD O/T HRS
                        columns.RelativeColumn(0.7f); // SAT O/T HRS
                        columns.RelativeColumn(0.7f); // SUN O/T HRS
                    }
                    if (IsColVisible("Loans")) columns.RelativeColumn(0.9f);
                    if (IsColVisible("Washing")) columns.RelativeColumn(0.9f);
                    if (IsColVisible("Gas")) columns.RelativeColumn(0.9f);
                    if (IsColVisible("Other")) columns.RelativeColumn(0.9f);
                    if (IsColVisible("OccToBibc")) columns.RelativeColumn(1.1f); // OCC to BIBC
                    if (IsColVisible("TotalNett")) columns.RelativeColumn(1.2f);
                    if (IsColVisible("Bank")) columns.RelativeColumn(1.1f);
                    if (IsColVisible("BankAccount")) columns.RelativeColumn(1.6f);
                    if (IsColVisible("Comments")) columns.RelativeColumn(1.8f);
                    if (IsColVisible("Notes")) columns.RelativeColumn(1.8f);
                    if (IsColVisible("TotalRem")) columns.RelativeColumn(1.2f);
                    if (IsColVisible("Days"))
                    {
                        columns.RelativeColumn(1.0f); // RATE P/DAY
                        columns.RelativeColumn(0.5f); // W1
                        columns.RelativeColumn(0.5f); // W2
                        columns.RelativeColumn(0.5f); // W3
                        columns.RelativeColumn(0.7f); // TOT D
                    }
                });

                table.Header(header =>
                {
                    if (IsColVisible("Index")) header.Cell().Element(WageHeaderStyle).Text("#");
                    if (IsColVisible("Bas")) header.Cell().Element(WageHeaderStyle).Text("BAS");
                    if (IsColVisible("Name")) header.Cell().Element(WageHeaderStyle).Text("NAME");
                    if (IsColVisible("RateHr")) header.Cell().Element(WageHeaderStyle).Text("RATE\nP/HR");
                    if (IsColVisible("Hrs")) header.Cell().Element(WageHeaderStyle).Text("HRS");
                    if (IsColVisible("OtRates"))
                    {
                        header.Cell().Element(WageHeaderStyle).Text("STD O/T\nRATE");
                        header.Cell().Element(WageHeaderStyle).Text("SAT O/T\nRATE");
                        header.Cell().Element(WageHeaderStyle).Text("SUN-P'HOL\nRATE");
                    }
                    if (IsColVisible("DecColumns"))
                    {
                        header.Cell().Element(WageHeaderStyle).Text("DEC O/T\nRATE");
                        header.Cell().Element(WageHeaderStyle).Text("DEC O/T\nHRS");
                        header.Cell().Element(WageHeaderStyle).Text("DEC\nTOTAL");
                    }
                    if (IsColVisible("OtHours"))
                    {
                        header.Cell().Element(WageHeaderStyle).Text("STD\nO/T");
                        header.Cell().Element(WageHeaderStyle).Text("SAT\nO/T");
                        header.Cell().Element(WageHeaderStyle).Text("SUN\nO/T");
                    }
                    if (IsColVisible("Loans")) header.Cell().Element(WageHeaderStyle).Text("LOANS");
                    if (IsColVisible("Washing")) header.Cell().Element(WageHeaderStyle).Text("WASH-\nING");
                    if (IsColVisible("Gas")) header.Cell().Element(WageHeaderStyle).Text("GAS");
                    if (IsColVisible("Other")) header.Cell().Element(WageHeaderStyle).Text("OTHER");
                    if (IsColVisible("OccToBibc")) header.Cell().Element(WageHeaderStyle).Text("OCC to\nBIBC");
                    if (IsColVisible("TotalNett")) header.Cell().Element(WageHeaderStyle).Text("TOTAL\nNETT");
                    if (IsColVisible("Bank")) header.Cell().Element(WageHeaderStyle).Text("BANK");
                    if (IsColVisible("BankAccount")) header.Cell().Element(WageHeaderStyle).Text("ACCOUNT\nNUMBER");
                    if (IsColVisible("Comments")) header.Cell().Element(WageHeaderStyle).Text("COMMENTS");
                    if (IsColVisible("Notes")) header.Cell().Element(WageHeaderStyle).Text("NOTES");
                    if (IsColVisible("TotalRem")) header.Cell().Element(WageHeaderStyle).Text("TOTAL\nREM");
                    if (IsColVisible("Days"))
                    {
                        header.Cell().Element(WageHeaderStyle).Text("RATE\nP/DAY");
                        header.Cell().Element(WageHeaderStyle).Text("WEEK 1");
                        header.Cell().Element(WageHeaderStyle).Text("WEEK 2");
                        header.Cell().Element(WageHeaderStyle).Text("WEEK 3");
                        header.Cell().Element(WageHeaderStyle).Text("TOTAL\nDAYS");
                    }

                    static IContainer WageHeaderStyle(IContainer c) =>
                        c.Border(0.5f).Background(Colors.Grey.Lighten4).PaddingVertical(3).PaddingHorizontal(2)
                         .AlignCenter().AlignMiddle()
                         .DefaultTextStyle(x => x.Bold().FontSize(6.0f));
                });

                int index = 1;
                foreach (var line in lines)
                {
                    if (IsColVisible("Index")) table.Cell().Element(WageCellStyle).Text(index.ToString());
                    index++;
                    if (IsColVisible("Bas")) table.Cell().Element(WageCellStyle).Text(line.EmployeeNumber ?? "");
                    if (IsColVisible("Name")) table.Cell().Element(WageCellStyle).Text(line.EmployeeName ?? "");
                    if (IsColVisible("RateHr")) table.Cell().Element(WageCellStyle).AlignRight().Text(line.HourlyRate.ToString("F2"));
                    if (IsColVisible("Hrs"))
                    {
                        decimal stdHours = (decimal)(line.NormalHours + line.ProjectedHours + line.VarianceHours);
                        table.Cell().Element(WageCellStyle).AlignCenter().Text(stdHours.ToString("F2"));
                    }
                    if (IsColVisible("OtRates"))
                    {
                        table.Cell().Element(WageCellStyle).AlignRight().Text((line.HourlyRate * 1.5m).ToString("F2"));
                        table.Cell().Element(WageCellStyle).AlignRight().Text((line.HourlyRate * 1.5m).ToString("F2"));
                        table.Cell().Element(WageCellStyle).AlignRight().Text((line.HourlyRate * 2.0m).ToString("F2"));
                    }
                    if (IsColVisible("DecColumns"))
                    {
                        table.Cell().Element(WageCellStyle).AlignRight().Text(line.HourlyRate.ToString("F2"));
                        table.Cell().Element(WageCellStyle).AlignCenter().Text("0.00");
                        table.Cell().Element(WageCellStyle).AlignRight().Text("0.00");
                    }
                    if (IsColVisible("OtHours"))
                    {
                        table.Cell().Element(WageCellStyle).AlignCenter().Text(line.Overtime15Hours.ToString("F2"));
                        table.Cell().Element(WageCellStyle).AlignCenter().Text(line.SaturdayOvertimeHours.ToString("F2"));
                        table.Cell().Element(WageCellStyle).AlignCenter().Text(line.Overtime20Hours.ToString("F2"));
                    }
                    if (IsColVisible("Loans")) table.Cell().Element(WageCellStyle).AlignRight().Text(line.DeductionLoan.ToString("F2"));
                    if (IsColVisible("Washing")) table.Cell().Element(WageCellStyle).AlignRight().Text(line.DeductionWashing.ToString("F2"));
                    if (IsColVisible("Gas")) table.Cell().Element(WageCellStyle).AlignRight().Text(line.DeductionGas.ToString("F2"));
                    if (IsColVisible("Other"))
                    {
                        decimal otherTotal = line.DeductionOther + line.DeductionPPE;
                        table.Cell().Element(WageCellStyle).AlignRight().Text(otherTotal.ToString("F2"));
                    }
                    if (IsColVisible("OccToBibc"))
                    {
                        table.Cell().Element(WageCellStyle).AlignRight().Text(line.BibcAmount.ToString("F2"));
                    }
                    if (IsColVisible("TotalNett")) table.Cell().Element(WageCellStyle).AlignRight().Text(line.NetPay.ToString("F2")).SemiBold();
                    if (IsColVisible("Bank")) table.Cell().Element(WageCellStyle).Text(line.BankName ?? "");
                    if (IsColVisible("BankAccount")) table.Cell().Element(WageCellStyle).Text(line.BankAccountNumber ?? "");
                    if (IsColVisible("Comments")) table.Cell().Element(WageCellStyle).Text(line.Comments ?? "");
                    if (IsColVisible("Notes")) table.Cell().Element(WageCellStyle).Text(line.VarianceNotes ?? "");
                    if (IsColVisible("TotalRem")) table.Cell().Element(WageCellStyle).AlignRight().Text(line.TotalWage.ToString("F2"));
                    if (IsColVisible("Days"))
                    {
                        table.Cell().Element(WageCellStyle).AlignRight().Text((line.HourlyRate * 8.75m).ToString("F2"));
                        table.Cell().Element(WageCellStyle).AlignCenter().Text(line.DaysWorkedWeek1.ToString("0.#"));
                        table.Cell().Element(WageCellStyle).AlignCenter().Text(line.DaysWorkedWeek2.ToString("0"));
                        table.Cell().Element(WageCellStyle).AlignCenter().Text(line.DaysWorkedWeek3.ToString("0"));
                        table.Cell().Element(WageCellStyle).AlignCenter().Text(line.TotalDaysWorked.ToString("0"));
                    }

                    if (line.IncentiveSupervisor > 0 && IsColVisible("SupFee"))
                    {
                        if (visibleColCountBeforeNett > 0)
                        {
                            table.Cell().ColumnSpan((uint)visibleColCountBeforeNett).Element(WageSubRowStyle).AlignRight().PaddingRight(2).Text("SUPERVISOR FEE").Bold().FontSize(6.0f);
                        }
                        if (IsColVisible("TotalNett"))
                        {
                            table.Cell().Element(WageSubRowStyle).AlignRight().Text($"R {line.IncentiveSupervisor:F2}").Bold().FontSize(6.0f);
                        }
                        if (visibleColCountAfterNett > 0)
                        {
                            table.Cell().ColumnSpan((uint)visibleColCountAfterNett).Element(WageSubRowStyle);
                        }
                    }

                    static IContainer WageCellStyle(IContainer c) =>
                        c.Border(0.5f).PaddingVertical(2.5f).PaddingHorizontal(2).AlignMiddle();

                    static IContainer WageSubRowStyle(IContainer c) =>
                        c.BorderLeft(0.5f).BorderRight(0.5f).BorderBottom(0.5f).Background(Colors.Grey.Lighten3).PaddingVertical(2.5f).PaddingHorizontal(2).AlignMiddle();
                }

                table.Footer(footer =>
                {
                    if (visibleColCountBeforeNett > 0)
                    {
                        footer.Cell().ColumnSpan((uint)visibleColCountBeforeNett).Element(c => c.AlignRight().PaddingRight(5).Text("TOTAL:").Bold());
                    }
                    if (IsColVisible("TotalNett"))
                    {
                        footer.Cell().Element(c => c.Border(0.5f).PaddingVertical(2.5f).PaddingHorizontal(2).AlignRight()
                             .Text(lines.Sum(x => x.NetPay).ToString("F2")).Bold());
                    }
                    if (visibleColCountAfterNett > 0)
                    {
                        footer.Cell().ColumnSpan((uint)visibleColCountAfterNett).Element(c => c.Border(0.5f));
                    }
                });
            });
        }




        private void ComposeWageTotalsTable(IContainer container, WageRun wageRun)
        {
            bool isCpt = wageRun.Branch.ToBranchEnum() == Branch.CPT;

            container.Column(col =>
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(55); // LOANS
                        if (!isCpt)
                        {
                            columns.ConstantColumn(55); // WASHING
                            columns.ConstantColumn(55); // GAS
                        }
                        else
                        {
                            columns.ConstantColumn(55); // OTHER
                        }
                        columns.ConstantColumn(55); // BIBC (or LIVING OUT)
                        columns.ConstantColumn(65); // TOTAL
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(c => c.Border(0.5f));
                        header.Cell().Element(WageTotStyle).Text("LOANS");
                        if (!isCpt)
                        {
                            header.Cell().Element(WageTotStyle).Text("WASHING");
                            header.Cell().Element(WageTotStyle).Text("GAS");
                        }
                        else
                        {
                            header.Cell().Element(WageTotStyle).Text("OTHER");
                        }
                        header.Cell().Element(WageTotStyle).Text(isCpt ? "BIBC" : "LIVING OUT");
                        header.Cell().Element(WageTotStyle).Text("TOTAL");

                        static IContainer WageTotStyle(IContainer c) =>
                            c.Border(0.5f).AlignCenter().DefaultTextStyle(x => x.Bold().FontSize(6.5f));
                    });

                    var permLines   = wageRun.Lines.Where(l => l.EmploymentType == "Permanent").ToList();
                    var casualLines = wageRun.Lines.Where(l => l.EmploymentType != "Permanent").ToList();

                    AddWageTotalRow(table, "Permanent Staff", permLines, isCpt);
                    AddWageTotalRow(table, "Casual Staff",    casualLines, isCpt);

                    // Grand Total
                    table.Cell().Element(WageTotLineStyle).Text("Total").Bold();
                    table.Cell().Element(WageTotLineStyle).AlignRight().Text(wageRun.Lines.Sum(x => x.DeductionLoan).ToString("F2")).Bold();
                    if (!isCpt)
                    {
                        table.Cell().Element(WageTotLineStyle).AlignRight().Text(wageRun.Lines.Sum(x => x.DeductionWashing).ToString("F2")).Bold();
                        table.Cell().Element(WageTotLineStyle).AlignRight().Text(wageRun.Lines.Sum(x => x.DeductionGas).ToString("F2")).Bold();
                    }
                    else
                    {
                        table.Cell().Element(WageTotLineStyle).AlignRight().Text(wageRun.Lines.Sum(x => x.DeductionOther).ToString("F2")).Bold();
                    }
                    table.Cell().Element(WageTotLineStyle).AlignRight().Text(isCpt ? wageRun.Lines.Sum(x => x.BibcAmount).ToString("F2") : "0.00").Bold();
                    table.Cell().Element(WageTotLineStyle).AlignRight()
                        .Text(wageRun.Lines.Sum(x => x.NetPay).ToString("F2")).Bold();

                    static void AddWageTotalRow(TableDescriptor t, string label, List<WageRunLine> ls, bool isCpt)
                    {
                        t.Cell().Element(WageTotLineStyle).Text(label);
                        t.Cell().Element(WageTotLineStyle).AlignRight().Text(ls.Sum(x => x.DeductionLoan).ToString("F2"));
                        if (!isCpt)
                        {
                            t.Cell().Element(WageTotLineStyle).AlignRight().Text(ls.Sum(x => x.DeductionWashing).ToString("F2"));
                            t.Cell().Element(WageTotLineStyle).AlignRight().Text(ls.Sum(x => x.DeductionGas).ToString("F2"));
                        }
                        else
                        {
                            t.Cell().Element(WageTotLineStyle).AlignRight().Text(ls.Sum(x => x.DeductionOther).ToString("F2"));
                        }
                        t.Cell().Element(WageTotLineStyle).AlignRight().Text(isCpt ? ls.Sum(x => x.BibcAmount).ToString("F2") : "0.00");
                        t.Cell().Element(WageTotLineStyle).AlignRight().Text(ls.Sum(x => x.NetPay).ToString("F2"));
                    }

                    static IContainer WageTotLineStyle(IContainer c) =>
                        c.Border(0.5f).PaddingHorizontal(2).AlignMiddle().DefaultTextStyle(x => x.FontSize(6.5f));
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
                    var loansTotal = 0m; // Loans are already deducted from individual NetPay
                    var grandTotal = permNet + casualNet;

                    table.Cell().Element(RowStyle).Text("Permanent Staff").Bold();
                    table.Cell().Element(RowStyle).AlignRight().Text(permNet.ToString("R #,##0.00"));

                    table.Cell().Element(RowStyle).Text("Casual / Temp Staff").Bold();
                    table.Cell().Element(RowStyle).AlignRight().Text(casualNet.ToString("R #,##0.00"));

                    table.Cell().Element(RowStyle).Text(isCpt ? "BIBC" : "Loans").Bold();
                    table.Cell().Element(RowStyle).AlignRight().Text(isCpt ? wageRun.Lines.Sum(x => x.BibcAmount).ToString("R #,##0.00") : loansTotal.ToString("R #,##0.00"));

                    table.Cell().Element(RowStyle).Text("Total of Wage Run").Bold();
                    table.Cell().Element(RowStyle).AlignRight().Text(grandTotal.ToString("R #,##0.00")).Bold();

                    static IContainer RowStyle(IContainer c) =>
                        c.Border(0.5f).Padding(2).AlignMiddle().DefaultTextStyle(x => x.FontSize(7.5f));
                });
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
                col.Item().Text("3. An administration fee is calculated as specified. Early repayment is permitted without penalty.");
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
                    row.RelativeItem().Text("Admin Fee:").SemiBold();
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
                        r.RelativeItem().Text("Admin Fee:").SemiBold();
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

        public async Task<string> GenerateWeeklyAttendanceReportPdfAsync(string title, string branchFilter, string searchFilter, List<WeeklyAttendanceReportWeekModel> weeks)
        {
            var company = new CompanyDetails();

            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(15);
                        page.Size(PageSizes.A4.Landscape());
                        page.DefaultTextStyle(x => x.FontSize(6).FontFamily(Fonts.Arial).FontColor(Colors.Black));

                        page.Header().Element(c => ComposeWeeklyAttendanceHeader(c, title, branchFilter, searchFilter, company));
                        page.Content().PaddingVertical(10).Element(c => ComposeWeeklyAttendanceContent(c, weeks));
                        page.Footer().Element(c => ComposeWeeklyAttendanceFooter(c, company));
                    });
                });

                string docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OCC", "AttendanceReports");
                if (!Directory.Exists(docsPath)) Directory.CreateDirectory(docsPath);

                string filename = $"Weekly_Attendance_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string fullPath = Path.Combine(docsPath, filename);
                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        private void ComposeWeeklyAttendanceHeader(IContainer container, string title, string branchFilter, string searchFilter, CompanyDetails company)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    // Left
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(company.CompanyName).FontSize(12).ExtraBold().FontColor(ColorPrimary);
                        c.Item().Text(title.ToUpper()).FontSize(9).Bold().FontColor(ColorSecondary);
                    });

                    // Right (Filters and Date)
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        var filters = new List<string>();
                        if (!string.IsNullOrEmpty(branchFilter) && branchFilter != "All Branches" && branchFilter != "All")
                            filters.Add($"Branch: {branchFilter}");
                        if (!string.IsNullOrEmpty(searchFilter))
                            filters.Add($"Search: {searchFilter}");

                        string filterStr = filters.Count > 0 ? string.Join(" | ", filters) : "All Records";
                        c.Item().Text(filterStr).FontSize(7.5f).SemiBold().FontColor(Colors.Grey.Darken2);
                        c.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(7).FontColor(Colors.Grey.Medium);
                    });
                });
                col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            });
        }

        private void ComposeWeeklyAttendanceContent(IContainer container, List<WeeklyAttendanceReportWeekModel> weeks)
        {
            container.Column(col =>
            {
                for (int w = 0; w < weeks.Count; w++)
                {
                    var week = weeks[w];
                    if (w > 0)
                    {
                        col.Item().PageBreak();
                    }

                    // Week Title Header
                    col.Item().PaddingBottom(5).Row(row =>
                    {
                        string dateRangeStr = (week.FilterFromDate.HasValue && week.FilterToDate.HasValue)
                            ? $" ({week.FilterFromDate.Value:dd MMM yyyy} to {week.FilterToDate.Value:dd MMM yyyy})"
                            : "";
                        row.RelativeItem().Text($"DAILY ATTENDANCE REGISTER{dateRangeStr} - WEEK ENDING {week.WeekEnd:yyyy/MM/dd}").FontSize(9).ExtraBold().FontColor(ColorPrimary);
                        row.RelativeItem().AlignRight().Text($"Period: {week.WeekStart:dd MMM yyyy} to {week.WeekEnd:dd MMM yyyy}").FontSize(8).SemiBold();
                    });

                    // Weekly table
                    col.Item().Element(c => ComposeWeeklyAttendanceTable(c, week));
                }
            });
        }

        private void ComposeWeeklyAttendanceTable(IContainer container, WeeklyAttendanceReportWeekModel week)
        {
            var employees = week.Employees;
            container.Table(table =>
            {
                // Columns Definition (29 columns total: Name + 7 days * 4 sub-columns)
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.0f); // NAME
                    for (int d = 0; d < 7; d++)
                    {
                        columns.RelativeColumn(1.3f); // Site
                        columns.ConstantColumn(20);   // In
                        columns.ConstantColumn(20);   // Out
                        columns.ConstantColumn(16);   // O/T
                    }
                });

                // Header Row 1: Name and Day headers
                table.Header(header =>
                {
                    static IContainer MainHeaderStyle(IContainer c) =>
                        c.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(ColorPrimary).Padding(2)
                         .AlignCenter().AlignMiddle()
                         .DefaultTextStyle(x => x.Bold().FontSize(6.5f).FontColor(Colors.White));

                    static IContainer SubHeaderStyle(IContainer c) =>
                         c.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(1)
                          .AlignCenter().AlignMiddle()
                          .DefaultTextStyle(x => x.Bold().FontSize(5.5f).FontColor(ColorSecondary));

                    header.Cell().RowSpan(2).Element(MainHeaderStyle).Text("NAME");
                    
                    var daysOfWeek = new[] { "SAT", "SUN", "MON", "TUE", "WED", "THUR", "FRI" };
                    for (int i = 0; i < 7; i++)
                    {
                        var dayDate = week.WeekStart.AddDays(i);
                        string dayHeader = $"{daysOfWeek[i]} - {GetDayWithSuffix(dayDate)}";
                        header.Cell().ColumnSpan(4).Element(MainHeaderStyle).Text(dayHeader);
                    }

                    // Header Row 2: Sub-headers Site, In, Out, O/T
                    for (int d = 0; d < 7; d++)
                    {
                        header.Cell().Element(SubHeaderStyle).Text("Site");
                        header.Cell().Element(SubHeaderStyle).Text("In");
                        header.Cell().Element(SubHeaderStyle).Text("Out");
                        header.Cell().Element(SubHeaderStyle).Text("O/T");
                    }
                });

                // Rows
                bool printedPermanentHeader = false;
                bool printedCasualHeader = false;

                foreach (var emp in employees)
                {
                    if (emp.EmploymentType != "Contract" && !printedPermanentHeader)
                    {
                        printedPermanentHeader = true;
                        table.Cell().ColumnSpan(29).Background(Colors.Grey.Lighten3).Padding(2).AlignLeft().AlignMiddle()
                            .Text("  PERMANENT EMPLOYEES").FontSize(6.5f).Bold().FontColor(ColorPrimary);
                    }
                    else if (emp.EmploymentType == "Contract" && !printedCasualHeader)
                    {
                        printedCasualHeader = true;
                        table.Cell().ColumnSpan(29).Background(Colors.Grey.Lighten3).Padding(2).AlignLeft().AlignMiddle()
                            .Text("  CASUAL / CONTRACT EMPLOYEES").FontSize(6.5f).Bold().FontColor(ColorPrimary);
                    }

                    static IContainer NameStyle(IContainer c) =>
                        c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignLeft().AlignMiddle();

                    static IContainer CellStyle(IContainer c) =>
                        c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignCenter().AlignMiddle();

                    static IContainer SiteStyle(IContainer c) =>
                        c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignLeft().AlignMiddle();

                    table.Cell().Element(NameStyle).Text(emp.EmployeeName).Bold();

                    for (int d = 0; d < 7; d++)
                    {
                        var dayData = emp.Days[d] ?? new DailyAttendancePrintModel();
                        
                        // SiteName
                        table.Cell().Element(SiteStyle).Text(dayData.Site);
                        // In
                        table.Cell().Element(CellStyle).Text(dayData.TimeIn);
                        // Out
                        table.Cell().Element(CellStyle).Text(dayData.TimeOut);
                        // O/T
                        table.Cell().Element(CellStyle).Text(dayData.Overtime);
                    }
                }
            });
        }

        private void ComposeWeeklyAttendanceFooter(IContainer container, CompanyDetails company)
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

        public async Task<string> GenerateSickLeaveReportPdfAsync(OCC.Shared.DTOs.EmployeeDto employee, IEnumerable<LeaveRequest> sickLeaves, IEnumerable<AttendanceRecord> sickDays)
        {
            var company = new CompanyDetails();
            var empName = $"{employee.FirstName} {employee.LastName}";
            
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                        page.Header().Element(c => ComposeGenericHeader(c, $"SICK LEAVE SUMMARY REPORT", company));

                        page.Content().PaddingVertical(16).Column(col =>
                        {
                            // 1. Employee Info Card
                            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Column(box =>
                            {
                                box.Item().Background(ColorPrimary).Padding(8)
                                   .Text("EMPLOYEE INFORMATION").SemiBold().FontColor(Colors.White).FontSize(10);
                                box.Item().Padding(12).Row(row =>
                                {
                                    row.RelativeItem().Column(c2 =>
                                    {
                                        c2.Item().Text(t => { t.Span("Full Name:  ").SemiBold(); t.Span(empName); });
                                        c2.Item().PaddingTop(4).Text(t => { t.Span("Employee No: ").SemiBold(); t.Span(employee.EmployeeNumber ?? "—"); });
                                        c2.Item().PaddingTop(4).Text(t => { t.Span("Role / Trade:  ").SemiBold(); t.Span(employee.Role.ToString()); });
                                    });
                                    row.RelativeItem().Column(c2 =>
                                    {
                                        c2.Item().Text(t => { t.Span("Branch:  ").SemiBold(); t.Span(employee.Branch ?? "—"); });
                                        c2.Item().PaddingTop(4).Text(t => { t.Span("Cycle Start: ").SemiBold(); t.Span(employee.LeaveCycleStartDate?.ToString("dd MMM yyyy") ?? "—"); });
                                        c2.Item().PaddingTop(4).Text(t => { t.Span("Sick Balance: ").SemiBold(); t.Span($"{employee.SickLeaveBalance:F1} days").Bold().FontColor(ColorPrimary); });
                                    });
                                });
                            });

                            col.Item().Height(14);

                            // 2. Sick Leave Applications Table
                            col.Item().Text("APPROVED SICK LEAVE APPLICATIONS").Bold().FontSize(11).FontColor(ColorSecondary);
                            col.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Lighten2).Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.2f); // From
                                    cols.RelativeColumn(1.2f); // To
                                    cols.RelativeColumn(1.8f); // Reason
                                    cols.RelativeColumn(0.8f); // Days
                                    cols.RelativeColumn(0.8f); // Paid
                                    cols.RelativeColumn(0.8f); // Unpaid
                                    cols.RelativeColumn(1.5f); // Note?
                                });
                                
                                table.Header(h =>
                                {
                                    foreach (var hdr in new[] { "FROM", "TO", "REASON", "DAYS", "PAID", "UNPAID", "DOCTOR'S NOTE" })
                                        h.Cell().Background(ColorLightOrange).BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten2).Padding(6)
                                         .Text(hdr).SemiBold().FontSize(8).FontColor(ColorSecondary);
                                });

                                foreach (var lr in sickLeaves)
                                {
                                    table.Cell().Padding(6).Text(lr.StartDate.ToString("dd MMM yyyy"));
                                    table.Cell().Padding(6).Text(lr.EndDate.ToString("dd MMM yyyy"));
                                    table.Cell().Padding(6).Text(lr.Reason ?? "Sick Leave");
                                    table.Cell().Padding(6).Text(lr.NumberOfDays.ToString("F1"));
                                    table.Cell().Padding(6).Text(lr.PaidDays.ToString("F1"));
                                    table.Cell().Padding(6).Text(lr.UnpaidDays.ToString("F1"));
                                    table.Cell().Padding(6).Text(!string.IsNullOrEmpty(lr.DoctorsNoteImagePath) ? "Yes (Attached)" : "No");
                                }
                                
                                if (!sickLeaves.Any())
                                {
                                    table.Cell().ColumnSpan(7).Padding(12).AlignCenter().Text("No sick leave records found.").Italic().FontColor(Colors.Grey.Medium);
                                }
                            });

                            col.Item().Height(14);

                            // 3. Daily Attendance Breakdown Table
                            col.Item().Text("DAILY SICK ATTENDANCE BREAKDOWN").Bold().FontSize(11).FontColor(ColorSecondary);
                            col.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Lighten2).Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.5f); // Date
                                    cols.RelativeColumn(1.5f); // Status
                                    cols.RelativeColumn(1.0f); // Paid Hours
                                    cols.RelativeColumn(2.5f); // Notes
                                    cols.RelativeColumn(1.5f); // Paid in Wage Run
                                });
                                
                                table.Header(h =>
                                {
                                    foreach (var hdr in new[] { "DATE", "STATUS", "PAID HOURS", "NOTES", "WAGE RUN" })
                                        h.Cell().Background(ColorLightOrange).BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten2).Padding(6)
                                         .Text(hdr).SemiBold().FontSize(8).FontColor(ColorSecondary);
                                });

                                foreach (var day in sickDays.OrderByDescending(d => d.Date))
                                {
                                    table.Cell().Padding(6).Text(day.Date.ToString("dd MMM yyyy (ddd)"));
                                    table.Cell().Padding(6).Text(day.Status.ToString());
                                    table.Cell().Padding(6).Text((day.PaidLeaveHours ?? 0.0).ToString("F1"));
                                    string notes = day.Notes ?? "—";
                                    if (!string.IsNullOrEmpty(notes))
                                    {
                                        notes = System.Text.RegularExpressions.Regex.Replace(notes, @"\s*\[[Ll]eave[Rr]equest:[0-9a-fA-F\-]{36}\]\s*", "").Trim();
                                        if (string.IsNullOrEmpty(notes)) notes = "—";
                                    }
                                    table.Cell().Padding(6).Text(notes);
                                    table.Cell().Padding(6).Text(day.PaidWageRunId.HasValue ? "Paid" : "Unpaid / Pending");
                                }
                                
                                if (!sickDays.Any())
                                {
                                    table.Cell().ColumnSpan(5).Padding(12).AlignCenter().Text("No daily sick days recorded in attendance.").Italic().FontColor(Colors.Grey.Medium);
                                }
                            });

                            col.Item().Height(30);

                            // 4. Signatures
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c2 =>
                                {
                                    c2.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).Height(40).Text("");
                                    c2.Item().PaddingTop(6).Text("Employee Signature").Bold().FontSize(9);
                                    c2.Item().Text("Date: ________________________").FontSize(8);
                                });
                                row.ConstantItem(50);
                                row.RelativeItem().Column(c2 =>
                                {
                                    c2.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).Height(40).Text("");
                                    c2.Item().PaddingTop(6).Text("Manager / Supervisor Signature").Bold().FontSize(9);
                                    c2.Item().Text("Date: ________________________").FontSize(8);
                                });
                            });
                        });

                        page.Footer().Element(c => ComposeGenericFooter(c, company));
                    });
                });

                string docsPath = Path.GetTempPath();
                string filename = $"SickLeaveReport_{employee.LastName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string fullPath = Path.Combine(docsPath, filename);

                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        private static string GetDayWithSuffix(DateTime date)
        {
            int day = date.Day;
            string suffix = (day % 10 == 1 && day != 11) ? "st"
                          : (day % 10 == 2 && day != 12) ? "nd"
                          : (day % 10 == 3 && day != 13) ? "rd"
                          : "th";
            return $"{day}{suffix}";
        }
    }
}
