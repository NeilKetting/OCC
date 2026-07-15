using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Infrastructure;
using OCC.Shared.Models;
using OCC.Shared.Enums;
using OCC.WpfClient.Features.HseqHub.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class AuditPdfMappingViewModel : OverlayViewModel
    {
        [ObservableProperty]
        private ObservableCollection<ParsedPdfRowViewModel> _rows = new();

        [ObservableProperty]
        private string _pdfFileName = string.Empty;

        public Guid? ProjectId { get; private set; }
        public string? ProjectName { get; private set; }
        public string? PdfFilePath { get; private set; }

        public List<string> StandardCategories { get; } = new()
        {
            "None",
            "Administrative Requirements",
            "Education Training & Promotion",
            "Public Safety",
            "Personal Protective Equipment (PPE)",
            "Housekeeping",
            "Elevated Work",
            "Electricity",
            "Fire Prevention and Protection",
            "Equipment",
            "Construction Vehicles and Mobile Plant",
            "Facilities"
        };

        public AuditPdfMappingViewModel()
        {
            Title = "Map PDF Audit Sections";
        }

        public void Initialize(string pdfPath, Guid? projectId, string? projectName)
        {
            PdfFilePath = pdfPath;
            PdfFileName = Path.GetFileName(pdfPath);
            ProjectId = projectId;
            ProjectName = projectName;

            var parsed = AuditPdfParser.ExtractRowsFromPdf(pdfPath);
            Rows.Clear();

            foreach (var item in parsed)
            {
                var autoMapped = AutoDetectCategory(item.PdfCategoryName);
                Rows.Add(new ParsedPdfRowViewModel
                {
                    PdfCategoryName = item.PdfCategoryName,
                    PossibleScore = item.PossibleScore,
                    AchievedScore = item.AchievedScore,
                    MappedCategory = autoMapped
                });
            }
        }

        [RelayCommand]
        public void ConfirmMapping()
        {
            // Build the HseqAudit object by summing the mapped rows
            var audit = new HseqAudit
            {
                Id = Guid.Empty,
                Date = DateTime.Today,
                Status = AuditStatus.InProgress,
                ProjectId = ProjectId,
                SiteName = ProjectName ?? string.Empty,
                TargetScore = 100,
                Sections = new List<HseqAuditSection>()
            };

            // Attempt to parse metadata fields directly from PDF text first as a fallback
            try
            {
                var textAudit = AuditPdfParser.ParseAuditPdf(PdfFilePath ?? string.Empty);
                audit.AuditNumber = textAudit.AuditNumber;
                audit.Date = textAudit.Date;
                if (!string.IsNullOrEmpty(textAudit.SiteName) && !ProjectId.HasValue)
                {
                    audit.SiteName = textAudit.SiteName;
                }
                audit.ScopeOfWorks = textAudit.ScopeOfWorks;
                audit.SiteManager = textAudit.SiteManager;
                audit.SiteSupervisor = textAudit.SiteSupervisor;
                audit.HseqConsultant = textAudit.HseqConsultant;
            }
            catch { }

            // Group the user-confirmed rows by mapped category
            var grouped = Rows
                .Where(r => r.MappedCategory != "None")
                .GroupBy(r => r.MappedCategory)
                .ToDictionary(g => g.Key, g => new
                {
                    Possible = g.Sum(r => r.PossibleScore),
                    Actual = g.Sum(r => r.AchievedScore)
                });

            // Standard categories (skip "None")
            foreach (var cat in StandardCategories.Skip(1))
            {
                int possible = 0;
                int actual = 0;

                if (grouped.TryGetValue(cat, out var scores))
                {
                    possible = scores.Possible;
                    actual = scores.Actual;
                }

                audit.Sections.Add(new HseqAuditSection
                {
                    Name = cat,
                    PossibleScore = possible,
                    ActualScore = actual
                });
            }

            // Recalculate actual summary score percentage
            decimal totalActual = 0;
            decimal totalPossible = 0;
            foreach (var section in audit.Sections)
            {
                totalActual += section.ActualScore;
                totalPossible += section.PossibleScore;
            }

            if (totalPossible > 0)
            {
                audit.ActualScore = Math.Min(100m, (totalActual / totalPossible) * 100m);
                audit.ActualScore = Math.Round(audit.ActualScore, 2);
            }
            else
            {
                audit.ActualScore = 0;
            }

            // Close mapping overlay and pass the parsed HseqAudit to the caller
            Close(audit);
        }

        private string AutoDetectCategory(string pdfName)
        {
            if (string.IsNullOrEmpty(pdfName)) return "None";

            var normalized = pdfName.ToLowerInvariant();

            if (normalized.Contains("admin") || normalized.Contains("legal"))
                return "Administrative Requirements";
            if (normalized.Contains("education") || normalized.Contains("training") || normalized.Contains("workers"))
                return "Education Training & Promotion";
            if (normalized.Contains("public safety"))
                return "Public Safety";
            if (normalized.Contains("protective") || normalized.Contains("ppe"))
                return "Personal Protective Equipment (PPE)";
            if (normalized.Contains("housekeeping"))
                return "Housekeeping";
            if (normalized.Contains("elevated") || normalized.Contains("working at height"))
                return "Elevated Work";
            if (normalized.Contains("electr"))
                return "Electricity";
            if (normalized.Contains("fire"))
                return "Fire Prevention and Protection";
            if (normalized.Contains("vehicle") || normalized.Contains("plant") || normalized.Contains("truck"))
                return "Construction Vehicles and Mobile Plant";
            if (normalized.Contains("facility") || normalized.Contains("facilities"))
                return "Facilities";
            if (normalized.Contains("equip"))
                return "Equipment";

            string bestMatch = "None";
            int maxCommonWords = 0;
            var pdfWords = normalized.Split(new[] { ' ', '&', '/', '-' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var std in StandardCategories.Skip(1))
            {
                var stdWords = std.ToLowerInvariant().Split(' ', '&', '/');
                int common = pdfWords.Intersect(stdWords).Count();
                if (common > maxCommonWords)
                {
                    maxCommonWords = common;
                    bestMatch = std;
                }
            }

            return maxCommonWords > 0 ? bestMatch : "None";
        }
    }

    public partial class ParsedPdfRowViewModel : ObservableObject
    {
        public string PdfCategoryName { get; set; } = string.Empty;
        public int PossibleScore { get; set; }
        public int AchievedScore { get; set; }

        [ObservableProperty]
        private string _mappedCategory = "None";
    }
}
