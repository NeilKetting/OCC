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
        private readonly ISettingsService _settingsService;

        // Brand Colors
        private static readonly string ColorPrimary = "#EF6C00"; // Orange
        private static readonly string ColorSecondary = "#374151"; // Dark Slate
        private static readonly string ColorLightOrange = "#FFF3E0";

        public PdfService(ISettingsService settingsService)
        {
             _settingsService = settingsService;
             // Configure community license
             QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<string> GenerateOrderPdfAsync(Order order, bool isPrintVersion = false)
        {
            // Fetch company details first (outside background thread to be safe with services)
            var company = await _settingsService.GetCompanyDetailsAsync();

            // Run on background thread to avoid UI freeze
            return await Task.Run(() =>
            {
                // Switch here if you ever need to fallback to Basic (true = Premium, false = Basic)
                bool usePremium = true;

                var doc = Document.Create(container =>
                {
                    if (usePremium)
                    {
                        ComposePremium(container, order, company, isPrintVersion);
                    }
                    else
                    {
                        ComposeBasic(container, order);
                    }
                });

                // Generate Path (Documents folder)
                string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string filename = $"Order_{order.OrderNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string fullPath = Path.Combine(docsPath, filename);

                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        #region Premium Design

        private void ComposePremium(IDocumentContainer container, Order order, CompanyDetails company, bool isPrint)
        {
            // Effective Colors
            var effectivePrimary = isPrint ? "#000000" : ColorPrimary;
            var effectiveLight = isPrint ? "#F5F5F5" : ColorLightOrange;

            container.Page(page =>
            {
                page.Margin(0); // Full bleed for header color
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(ColorSecondary));

                page.Header().SkipOnce().Element(c => ComposePremiumHeader(c, order, company, isPrint));
                page.Content().PaddingHorizontal(20).PaddingVertical(20).Element(c => ComposePremiumContent(c, order, company, isPrint));
                page.Footer().PaddingHorizontal(40).PaddingBottom(20).Element(c => ComposePremiumFooter(c, company));
            });
        }

        private void ComposePremiumHeader(IContainer container, Order order, CompanyDetails company, bool isPrint)
        {
            var effectivePrimary = isPrint ? "#000000" : ColorPrimary;

            container.PaddingTop(20).PaddingHorizontal(20).Row(row =>
            {
                // Left: Title and PO Number
                row.RelativeItem().Column(col =>
                {
                   col.Item().Text(order.OrderType == OrderType.PurchaseOrder ? "PURCHASE ORDER" : "ORDER")
                       .FontSize(20).ExtraBold().FontColor(Colors.Black); // Smaller font

                   col.Item().Text(order.OrderNumber).FontSize(16).SemiBold().FontColor(effectivePrimary);
                });

                // Right: Logo and Address
                row.RelativeItem().AlignRight().Column(c =>
                {
                     // Logo
                     byte[]? logoBytes = null;
                     try 
                     {
                         // Try Avalonia Resources first (embedded)
                         var uri = new Uri("avares://OCC.Client/Assets/Images/occ_logo.png");
                         if (Avalonia.Platform.AssetLoader.Exists(uri))
                         {
                             using var stream = Avalonia.Platform.AssetLoader.Open(uri);
                             using var ms = new MemoryStream();
                             stream.CopyTo(ms);
                             logoBytes = ms.ToArray();
                         }
                         // Fallback to file system
                         else 
                         {
                             var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "occ_logo.png");
                             if (File.Exists(logoPath))
                             {
                                 logoBytes = File.ReadAllBytes(logoPath);
                             }
                         }
                     }
                     catch (Exception) { /* Ignore load errors */ }

                     if (logoBytes != null)
                     {
                          c.Item().Height(80).AlignRight().Image(logoBytes).FitArea();
                     }
                     else
                     {
                          c.Item().AlignRight().Text(company.CompanyName.ToUpper()).FontSize(20).ExtraBold().FontColor(effectivePrimary);
                     }
                     
                     // Address (Moved from Content)
                     c.Item().PaddingTop(10).AlignRight().Text(company.CompanyName).Bold();
                     c.Item().AlignRight().Text(company.FullAddress);

                     if(!string.IsNullOrEmpty(company.RegistrationNumber))
                           c.Item().PaddingTop(2).AlignRight().Text($"Reg No: {company.RegistrationNumber}");
                     
                     c.Item().PaddingTop(2).AlignRight().Text($"Tel: {company.Phone}");
                     c.Item().AlignRight().Text($"Email: {company.Email}");
                });
            });
        }

        private void ComposePremiumContent(IContainer container, Order order, CompanyDetails company, bool isPrint)
        {
             // Resolve Branch Details once for the entire content
             var branchDetails = company.Branches.ContainsKey(order.Branch) 
                 ? company.Branches[order.Branch] 
                 : company.Branches[Branch.JHB];

             var effectivePrimary = isPrint ? "#000000" : ColorPrimary;

             // Master Layout for Page 1 (Includes Header Elements to control spacing)
             container.Column(column =>
             {
                 column.Item().Row(row =>
                 {
                     // LEFT COLUMN: Title -> PO -> Supplier -> ShipTo
                     row.ConstantItem(300).Column(col =>
                     {
                         // 1. Header: Title
                         col.Item().Text(order.OrderType == OrderType.PurchaseOrder ? "PURCHASE ORDER" : "ORDER")
                            .FontSize(20).ExtraBold().FontColor(Colors.Black);

                         // 2. GAP
                         col.Item().Height(15);

                         // 3. Supplier Box
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

                         col.Item().Height(10); // Spacer

                         // 4. Ship To Box
                         col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Column(box =>
                         {
                             box.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Ship To / Delivery").SemiBold();
                             box.Item().Padding(5).Column(details =>
                             {
                                 // Resolve Branch Details for Ship To
                                 var shipBranchDetails = company.Branches.ContainsKey(order.Branch) 
                                     ? company.Branches[order.Branch] 
                                     : company.Branches[Branch.JHB];

                                 details.Item().Text(company.CompanyName).SemiBold();
                                 details.Item().Text(shipBranchDetails.AddressLine1);
                                 if (!string.IsNullOrEmpty(shipBranchDetails.AddressLine2))
                                     details.Item().Text(shipBranchDetails.AddressLine2);
                                 details.Item().Text(shipBranchDetails.City);
                                 details.Item().Text(shipBranchDetails.PostalCode);
                             });
                         });
                     });

                     // Spacer between Left and Right main columns
                     row.ConstantItem(20);

                     // RIGHT COLUMN: Logo -> Address -> Date -> VAT
                     row.RelativeItem().AlignRight().Column(c =>
                     {
                          // Logo logic (Replicated for Page 1 Control)
                         byte[]? logoBytes = null;
                         try 
                         {
                             var uri = new Uri("avares://OCC.Client/Assets/Images/occ_logo.png");
                             if (Avalonia.Platform.AssetLoader.Exists(uri))
                             {
                                 using var stream = Avalonia.Platform.AssetLoader.Open(uri);
                                 using var ms = new MemoryStream();
                                 stream.CopyTo(ms);
                                 logoBytes = ms.ToArray();
                             }
                             else 
                             {
                                 var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "occ-logo.jpg");
                                 if (File.Exists(logoPath)) logoBytes = File.ReadAllBytes(logoPath);
                             }
                         }
                         catch (Exception) { }

                         if (logoBytes != null)
                             c.Item().Height(80).AlignRight().Image(logoBytes).FitArea();
                         else
                             c.Item().AlignRight().Text(company.CompanyName.ToUpper()).FontSize(20).ExtraBold().FontColor(effectivePrimary);
                         
                         // Address
                         c.Item().PaddingTop(2).AlignRight().Text(company.CompanyName).Bold();
                         c.Item().AlignRight().Text(branchDetails.AddressLine1);
                         if (!string.IsNullOrEmpty(branchDetails.AddressLine2))
                            c.Item().AlignRight().Text(branchDetails.AddressLine2);
                         c.Item().AlignRight().Text(branchDetails.City);
                         c.Item().AlignRight().Text(branchDetails.PostalCode);

                         // Space
                         c.Item().PaddingTop(8);

                         // Contact Info Group
                         c.Item().AlignRight().Text(t => { t.Span("Tel: ").SemiBold(); t.Span(branchDetails.Phone); });
                         if(!string.IsNullOrEmpty(branchDetails.Fax) && branchDetails.Fax != "TBD")
                             c.Item().AlignRight().Text(t => { t.Span("Fax: ").SemiBold(); t.Span(branchDetails.Fax); });
                         
                         // Space
                         c.Item().PaddingTop(8);

                         foreach(var dept in branchDetails.DepartmentEmails)
                         {
                             if (dept.EmailAddress != "TBD")
                                c.Item().AlignRight().Text(t => { t.Span($"{dept.Department}: ").SemiBold(); t.Span(dept.EmailAddress); });
                         }

                         // Reg No & VAT No Group
                         c.Item().PaddingTop(10).AlignRight().Column(meta => 
                         {
                             if(!string.IsNullOrEmpty(company.RegistrationNumber))
                                meta.Item().AlignRight().Text(t => { t.Span("Reg No: ").SemiBold(); t.Span(company.RegistrationNumber); });

                             if(!string.IsNullOrEmpty(company.VatNumber))
                                meta.Item().AlignRight().Text(t => { t.Span("VAT No: ").SemiBold(); t.Span(company.VatNumber); });
                         });
                     });
                 });

                 // Meta Row (Project, SOW, Date, PO)
                 column.Item().PaddingTop(20).Row(row =>
                 {
                     // 1. Project
                     row.RelativeItem(3).Text(t => { t.Span("PROJECT: ").SemiBold(); t.Span(order.ProjectName ?? "-"); });

                     // 2. SOW
                     row.RelativeItem(3).Text(t => { t.Span("SOW: ").SemiBold(); t.Span(string.IsNullOrEmpty(order.ScopeOfWork) ? "-" : order.ScopeOfWork); }); 

                     // 3. Date
                     row.RelativeItem(1.5f).Text(t => { t.Span("DATE: ").SemiBold(); t.Span($"{order.OrderDate:yyyy-MM-dd}"); });

                     // 4. PO No
                     row.RelativeItem(2).AlignRight().Text(t => { t.Span("PO No: ").SemiBold(); t.Span(order.OrderNumber); });
                 });

                 // Order Items Table
                 column.Item().PaddingTop(5).Element(c => ComposePremiumTable(c, order, isPrint));

                 // Bottom Section: Totals and Delivery Instructions
                 column.Item().PaddingTop(20).Row(row => 
                 {
                     row.ConstantItem(250).Element(c => ComposePremiumDeliveryInstructions(c, order));
                     row.RelativeItem(); // Spacer
                     row.ConstantItem(250).Element(c => ComposePremiumTotals(c, order, isPrint));
                 });
             });
        }

         private void ComposePremiumDeliveryInstructions(IContainer container, Order order)
         {
             container.Border(1).BorderColor(Colors.Grey.Lighten2).Column(instr =>
             {
                 instr.Item().Background(Colors.Grey.Lighten4).Padding(5).Text("Delivery Instructions").SemiBold().FontSize(9);
                 instr.Item().Padding(5).Text(!string.IsNullOrEmpty(order.DeliveryInstructions) ? order.DeliveryInstructions : "Please contact site manager before delivery.").FontSize(9);
             });
         }

        private void ComposePremiumTable(IContainer container, Order order, bool isPrint)
        {
             var effectivePrimary = isPrint ? "#000000" : ColorPrimary;

             container.Table(table =>
            {
                // Define Columns
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.ConstantColumn(80); // Code
                    columns.RelativeColumn();   // Description
                    columns.ConstantColumn(60); // Qty
                    columns.ConstantColumn(50); // Unit
                    columns.ConstantColumn(80); // Rate
                    columns.ConstantColumn(90); // Total
                });

                // Header
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

                // Items
                foreach (var line in order.Lines)
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
                 // Signatures
                 column.Item().PaddingBottom(20).Row(row =>
                 {
                     row.RelativeItem().Column(col => {
                          col.Item().PaddingBottom(5).Text("Authorized Signature").FontSize(9).FontColor(Colors.Grey.Medium);
                          col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                     });
                     
                     row.ConstantItem(40); // Spacer

                     row.RelativeItem().Column(col => {
                          col.Item().PaddingBottom(5).Text("Received By").FontSize(9).FontColor(Colors.Grey.Medium);
                          col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                     });
                 });

                 // Page Info and Generation Date
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

        #region Basic Design (Backup)

        private void ComposeBasic(IDocumentContainer container, Order order)
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Element(ComposeHeaderBasic);
                page.Content().Element(c => ComposeContentBasic(c, order));
                page.Footer().Element(ComposeFooterBasic);
            });
        }

        private void ComposeHeaderBasic(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Orange Circle Construction").FontSize(20).SemiBold().FontColor(Colors.Orange.Medium);
                    column.Item().Text("123 Construction Way");
                    column.Item().Text("Cape Town, 8001");
                    column.Item().Text("Tel: +27 21 555 1234");
                    column.Item().Text("Email: orders@orangecircle.co.za");
                });
            });
        }

        private void ComposeContentBasic(IContainer container, Order order)
        {
            container.PaddingVertical(40).Column(column =>
            {
                // Title
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(order.OrderType == OrderType.PurchaseOrder ? "PURCHASE ORDER" : "ORDER").FontSize(24).SemiBold().FontColor(Colors.Grey.Darken3);
                    row.RelativeItem().AlignRight().Text(order.OrderNumber).FontSize(24).SemiBold();
                });

                column.Item().PaddingTop(20).Row(row =>
                {
                    // Supplier Info
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("To:").SemiBold();
                        col.Item().Text(order.SupplierName ?? "Unknown Supplier");
                        if (!string.IsNullOrEmpty(order.EntityAddress))
                        {
                            col.Item().Text(order.EntityAddress);
                        }
                        if (!string.IsNullOrEmpty(order.EntityTel))
                        {
                            col.Item().Text($"Tel: {order.EntityTel}");
                        }
                         if (!string.IsNullOrEmpty(order.Attention))
                        {
                            col.Item().Text($"Attn: {order.Attention}");
                        }
                    });

                    // Order Info
                    row.ConstantItem(150).Column(col =>
                    {
                        col.Item().Text("Order Details:").SemiBold();
                        col.Item().Text($"Date: {order.OrderDate:yyyy-MM-dd}");
                        
                        if(order.ExpectedDeliveryDate.HasValue)
                             col.Item().Text($"Due: {order.ExpectedDeliveryDate:yyyy-MM-dd}");

                         col.Item().Text($"Dest: {order.DestinationType}");
                    });
                });

                // Table
                column.Item().PaddingTop(30).Element(c => ComposeTableBasic(c, order));

                // Totals
                column.Item().PaddingTop(20).Row(row =>
                {
                    row.RelativeItem().Component(new NotesComponent());
                    row.ConstantItem(200).Component(new TotalsComponent(order));
                });
                
                // Signatures
                 column.Item().PaddingTop(50).Row(row =>
                {
                    row.RelativeItem().Column(col => {
                         col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                         col.Item().Text("Approved By").FontSize(10);
                    });
                    
                    row.ConstantItem(50); // Spacer

                    row.RelativeItem().Column(col => {
                         col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                         col.Item().Text("Received By").FontSize(10);
                    });
                });
            });
        }

        private void ComposeTableBasic(IContainer container, Order order)
        {
            container.Table(table =>
            {
                // Define Columns
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);
                    columns.ConstantColumn(80); // Code
                    columns.RelativeColumn();   // Description
                    columns.ConstantColumn(60); // Qty
                    columns.ConstantColumn(60); // Unit
                    columns.ConstantColumn(80); // Rate
                    columns.ConstantColumn(80); // Total
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("#");
                    header.Cell().Element(CellStyle).Text("Code");
                    header.Cell().Element(CellStyle).Text("Description");
                    header.Cell().Element(CellStyle).AlignRight().Text("Qty");
                    header.Cell().Element(CellStyle).Text("Unit");
                    header.Cell().Element(CellStyle).AlignRight().Text("Rate");
                    header.Cell().Element(CellStyle).AlignRight().Text("Total");

                    static IContainer CellStyle(IContainer container)
                    {
                        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                    }
                });

                // Items
                foreach (var line in order.Lines)
                {
                    table.Cell().Element(CellStyle).Text((order.Lines.IndexOf(line) + 1).ToString());
                    table.Cell().Element(CellStyle).Text(line.ItemCode);
                    table.Cell().Element(CellStyle).Text(line.Description);
                    table.Cell().Element(CellStyle).AlignRight().Text($"{line.QuantityOrdered}");
                    table.Cell().Element(CellStyle).Text(line.UnitOfMeasure);
                    table.Cell().Element(CellStyle).AlignRight().Text($"{line.UnitPrice:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{line.LineTotal:N2}");

                    static IContainer CellStyle(IContainer container)
                    {
                        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                    }
                }
            });
        }

        private void ComposeFooterBasic(IContainer container)
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

        #endregion

        // Added for Employee Report
        public async Task<string> GenerateEmployeeReportPdfAsync<T>(Employee employee, DateTime start, DateTime end, IEnumerable<T> data, Dictionary<string, string> summary)
        {
            var company = await _settingsService.GetCompanyDetailsAsync();
            
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
                 // Left: Branding
                 row.RelativeItem(3).Column(col =>
                 {
                     col.Item().Text("Orange Circle Construction").FontSize(22).ExtraBold().FontColor(ColorPrimary);
                     col.Item().Text("Staff Performance Report").FontSize(12).FontColor(Colors.Grey.Medium);
                 });

                 // Right: Meta
                 row.RelativeItem(2).AlignRight().Column(col =>
                 {
                     col.Item().Text($"{start:dd MMM yyyy} - {end:dd MMM yyyy}").FontSize(14).SemiBold().FontColor(ColorSecondary);
                     col.Item().Text($"Generated: {DateTime.Now:g}").FontSize(9).FontColor(Colors.Grey.Medium);
                 });
             });
        }

        private void ComposeReportContent<T>(IContainer container, Employee employee, IEnumerable<T> data, Dictionary<string, string> summary)
        {
            container.Column(col =>
            {
                // Employee Info Row (No Border)
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
                
                // Summary Grid (Custom Layout)
                col.Item().PaddingTop(20).Element(c => ComposeSummaryGrid(c, summary));
                
                // Table
                col.Item().PaddingTop(30).Element(c => ComposeReportTable(c, data));
            });
        }

        private void ComposeSummaryGrid(IContainer container, Dictionary<string, string> summary)
        {
             // Extract specific values
             var totalHours = summary.ContainsKey("Total Hours") ? summary["Total Hours"] : "-";
             var totalOT = summary.ContainsKey("Total Overtime") ? summary["Total Overtime"] : "-";
             
             // Parsing composite values "Hours|Pay"
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
                 // 1. Total Hours
                 row.RelativeItem().PaddingRight(10).Element(c => 
                 {
                      c.Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten3).Padding(10).Column(col =>
                      {
                          col.Item().Text("TOTAL HOURS").FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);
                          col.Item().Text(totalHours).FontSize(14).SemiBold().FontColor(ColorSecondary);
                          col.Item().Text(normalPay).FontSize(10).FontColor(Colors.Grey.Medium); // Show Normal Pay here? User said "rand value of total normal hours"
                          // If "totalHours" includes OT, then normalPay (derived from total - OT) matches "Normal Hours".
                          // If the user meant "Value of the Total Hours displayed above", that is Gross Pay.
                          // But user said "rand value of the *total normal hours*". So likely specifically the Normal Pay.
                          // Visually:
                          // TOTAL HOURS
                          // 8.97
                          // R872.00 (Normal)
                      });
                 });
                 
                 // 2. Overtime Group
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
                 
                 // 3. Lates & Absences
                 row.RelativeItem().PaddingRight(10).Column(col => 
                 {
                     col.Item().Element(c => StatCard(c, "LATES", lates, "#FFEBEE", Colors.Red.Darken2)); // Red tint
                     col.Item().PaddingTop(10).Element(c => StatCard(c, "ABSENCES", absences, "#FFEBEE", Colors.Red.Darken2));
                 });
                 
                 // 4. Pay
                 row.RelativeItem().Element(c => StatCard(c, "GROSS PAY", pay, "#FFF3E0", ColorPrimary, true));
             });

             // Helper for Main Cards
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
             
             // Helper for Overtime Breakdown
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
            container.Column(col =>
            {
                 col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                 col.Item().PaddingTop(5).Row(row =>
                 {
                     row.RelativeItem().Text("Confidential Internal Report").FontSize(8).FontColor(Colors.Grey.Medium);
                     row.RelativeItem().AlignRight().Text(company.CompanyName).FontSize(8).FontColor(Colors.Grey.Medium);
                 });
            });
        }
    }
    
    public class NotesComponent : IComponent
    {
        public void Compose(IContainer container)
        {
            container.Background(Colors.Grey.Lighten4).Padding(10).Column(column =>
            {
                column.Item().Text("Notes:").SemiBold();
                column.Item().Text("Please implement deliveries between 8am and 4pm.");
                column.Item().Text("Quote PO number on all invoices.");
            });
        }
    }

    public class TotalsComponent : IComponent
    {
        private readonly Order _order;
        public TotalsComponent(Order order) => _order = order;

        public void Compose(IContainer container)
        {
             container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Subtotal:");
                    row.RelativeItem().AlignRight().Text($"{_order.SubTotal:N2}");
                });
                 column.Item().Row(row =>
                {
                    row.RelativeItem().Text("VAT (15%):");
                    row.RelativeItem().AlignRight().Text($"{_order.VatTotal:N2}");
                });
                
                column.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Total:").SemiBold().FontSize(14);
                    row.RelativeItem().AlignRight().Text($"{_order.TotalAmount:N2}").SemiBold().FontSize(14);
                });
            });
        }
    }
}
