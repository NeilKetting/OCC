using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OCC.Client.Services
{
    public partial class PdfService : IPdfService
    {
        public async Task<string> GenerateLoanSchedulePdfAsync(EmployeeLoan loan, Employee employee)
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

                        page.Header().Element(c => ComposeLoanHeader(c, employee, loan, company));
                        page.Content().PaddingVertical(20).Element(c => ComposeLoanContent(c, employee, loan));
                        page.Footer().Element(c => ComposeReportFooter(c, company));
                    });
                });

                string docsPath = Path.GetTempPath(); 
                string filename = $"Loan_{employee.LastName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string fullPath = Path.Combine(docsPath, filename);

                doc.GeneratePdf(fullPath);
                return fullPath;
            });
        }

        private void ComposeLoanHeader(IContainer container, Employee employee, EmployeeLoan loan, CompanyDetails company)
        {
             container.Row(row =>
             {
                 // Left: Branding
                 row.RelativeItem(3).Column(col =>
                 {
                     col.Item().Text("Orange Circle Construction").FontSize(22).ExtraBold().FontColor(ColorPrimary);
                     col.Item().Text("Employee Loan Agreement").FontSize(12).FontColor(Colors.Grey.Medium);
                 });

                 // Right: Meta
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
                // Employee Info
                col.Item().Background(Colors.Grey.Lighten5).Padding(15).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(employee.DisplayName).FontSize(16).Bold().FontColor(ColorSecondary);
                        c.Item().Text(employee.IdNumber).FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                     row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text(employee.Branch ?? "No Branch").FontSize(12).SemiBold().FontColor(Colors.Grey.Darken2);
                    });
                });

                // Loan Details Grid
                col.Item().PaddingTop(20).Element(c => ComposeLoanDetails(c, loan));
                
                // Terms
                col.Item().PaddingTop(30).Text("Terms and Conditions").FontSize(12).Bold().Underline();
                col.Item().PaddingTop(10).Text("1. The employee acknowledges the debt and agrees to repay the loan in the installments specified above.");
                col.Item().Text("2. The installments will be deducted directly from the employee's salary/wages.");
                col.Item().Text("3. Interest is calculated as specified. Early repayment is permitted without penalty.");
                col.Item().Text("4. If employment is terminated for any reason, the outstanding balance becomes immediately due and payable.");

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

        private void ComposeLoanDetails(IContainer container, EmployeeLoan loan)
        {
             container.Background(Colors.Grey.Lighten5).Border(1).BorderColor(Colors.Grey.Lighten3).Padding(15).Column(col =>
             {
                 col.Item().PaddingBottom(10).Text("Loan Details").FontSize(12).SemiBold();
                 
                 // Row 1
                 col.Item().Row(row =>
                 {
                     row.RelativeItem().Text("Principal Amount:").SemiBold();
                     row.RelativeItem().AlignRight().Text($"{loan.PrincipalAmount:C}");
                 });
                 
                 // Row 2
                 col.Item().PaddingTop(5).Row(row =>
                 {
                     row.RelativeItem().Text("Interest Rate:").SemiBold();
                     row.RelativeItem().AlignRight().Text($"{loan.InterestRate}%");
                 });
                 
                 // Row 3 (Installment)
                 col.Item().PaddingTop(5).Row(row =>
                 {
                     row.RelativeItem().Text("Installment Amount:").SemiBold();
                     row.RelativeItem().AlignRight().Text($"{loan.MonthlyInstallment:C}");
                 });
                 
                 col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                 
                 // Total Repayable (Approx) - If we want to show it. 
                 // We can re-calculate here to show the estimated total
                 // Simple Amortization logic solely for display
                 decimal totalRepayable = CalculateTotalRepayable(loan.PrincipalAmount, loan.MonthlyInstallment, loan.InterestRate);
                 
                 col.Item().Row(row =>
                 {
                     row.RelativeItem().Text("ESTIMATED TOTAL REPAYABLE:").Bold();
                     row.RelativeItem().AlignRight().Text($"{totalRepayable:C}").Bold();
                 });
             });
        }

        private decimal CalculateTotalRepayable(decimal principal, decimal installment, decimal annualInterestRate)
        {
            if (installment <= 0 || principal <= 0) return 0;
            if (annualInterestRate <= 0) return principal;

            double monthlyRate = (double)annualInterestRate / 100.0 / 12.0; // Assume monthly compounding for simplicity
            double p = (double)principal;
            double i = (double)installment;

            if (i <= p * monthlyRate) return 0; // Infinite

            // Number of months = -log(1 - (r * P) / I) / log(1 + r)
            double n = -Math.Log(1 - (monthlyRate * p) / i) / Math.Log(1 + monthlyRate);
            
            return (decimal)(n * i);
        }
    }
}
