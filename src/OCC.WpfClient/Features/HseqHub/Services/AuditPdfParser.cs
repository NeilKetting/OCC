using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using OCC.Shared.Models;
using OCC.Shared.Enums;

namespace OCC.WpfClient.Features.HseqHub.Services
{
    public static class AuditPdfParser
    {
        public static HseqAudit ParseAuditPdf(string pdfPath)
        {
            var audit = new HseqAudit
            {
                Id = Guid.Empty,
                Date = DateTime.Today,
                Status = AuditStatus.InProgress,
                TargetScore = 100,
                ActualScore = 0,
                Sections = new List<HseqAuditSection>()
            };

            var categories = new[]
            {
                "Administrative Requirements", "Education Training & Promotion", "Public Safety",
                "Personal Protective Equipment (PPE)", "Housekeeping", "Elevated Work", "Electricity",
                "Fire Prevention and Protection", "Equipment", "Construction Vehicles and Mobile Plant",
                "Facilities"
            };

            foreach (var cat in categories)
            {
                audit.Sections.Add(new HseqAuditSection
                {
                    Name = cat,
                    PossibleScore = 100,
                    ActualScore = 0
                });
            }

            if (!File.Exists(pdfPath))
                return audit;

            try
            {
                string text = "";
                using (var document = PdfDocument.Open(pdfPath))
                {
                    var pagesText = new List<string>();
                    for (int i = 1; i <= document.NumberOfPages; i++)
                    {
                        var page = document.GetPage(i);
                        pagesText.Add(page.Text);
                    }
                    text = string.Join("\n", pagesText);
                }

                if (string.IsNullOrWhiteSpace(text))
                    return audit;

                // 1. Audit Number
                var match = Regex.Match(text, @"(?i)Audit\s*(?:No\.?|Number|#)\s*:?\s*([A-Za-z0-9\-/\\]+)");
                if (match.Success)
                {
                    audit.AuditNumber = match.Groups[1].Value.Trim();
                }

                // 2. Date
                match = Regex.Match(text, @"(?i)Date\s*:?\s*([0-9]{4}[-/][0-9]{2}[-/][0-9]{2}|[0-9]{2}[-/][0-9]{2}[-/][0-9]{4}|[0-9]{1,2}\s+[A-Za-z]+\s+[0-9]{4})");
                if (match.Success)
                {
                    if (DateTime.TryParse(match.Groups[1].Value.Trim(), out var parsedDate))
                    {
                        audit.Date = parsedDate;
                    }
                }

                // 3. Site Name
                match = Regex.Match(text, @"(?i)Site(?:\s+Name)?\s*:?\s*([^\r\n]+)");
                if (match.Success)
                {
                    audit.SiteName = match.Groups[1].Value.Trim();
                }

                // 4. Scope of Works
                match = Regex.Match(text, @"(?i)Scope\s*(?:of\s*Works)?\s*:?\s*([^\r\n]+)");
                if (match.Success)
                {
                    audit.ScopeOfWorks = match.Groups[1].Value.Trim();
                }


                // 6. Site Supervisor
                match = Regex.Match(text, @"(?i)Site\s+Supervisor\s*:?\s*([^\r\n]+)");
                if (!match.Success)
                    match = Regex.Match(text, @"(?i)Supervisor\s*:?\s*([^\r\n]+)");
                if (match.Success)
                {
                    audit.SiteSupervisor = match.Groups[1].Value.Trim();
                }

                // 7. HSEQ Consultant
                match = Regex.Match(text, @"(?i)Hseq\s+Consultant\s*:?\s*([^\r\n]+)");
                if (!match.Success)
                    match = Regex.Match(text, @"(?i)Consultant\s*:?\s*([^\r\n]+)");
                if (match.Success)
                {
                    audit.HseqConsultant = match.Groups[1].Value.Trim();
                }

                // 8. Scores
                match = Regex.Match(text, @"(?i)Target\s*(?:Score)?\s*:?\s*(\d+(?:\.\d+)?)\s*%?");
                if (match.Success)
                {
                    if (decimal.TryParse(match.Groups[1].Value, out var target))
                    {
                        audit.TargetScore = target;
                    }
                }

                match = Regex.Match(text, @"(?i)Actual\s*(?:Score)?\s*:?\s*(\d+(?:\.\d+)?)\s*%?");
                if (!match.Success)
                    match = Regex.Match(text, @"(?i)(?:Score\s*Achieved|Achieved\s*Score|Final\s*Score|Result)\s*:?\s*(\d+(?:\.\d+)?)\s*%?");
                if (match.Success)
                {
                    if (decimal.TryParse(match.Groups[1].Value, out var actual))
                    {
                        audit.ActualScore = actual;
                    }
                }

                // 9. Section Scores
                foreach (var section in audit.Sections)
                {
                    // Find name index in text
                    int nameIndex = text.IndexOf(section.Name, StringComparison.OrdinalIgnoreCase);
                    if (nameIndex >= 0)
                    {
                        // Look for a percentage or a score format (e.g. 85 or 85% or 85/100) within the next 80 chars
                        string sub = text.Substring(nameIndex, Math.Min(80, text.Length - nameIndex));
                        var scoreMatch = Regex.Match(sub, @"\b(\d{1,3})\s*(?:%|/\s*100)\b");
                        if (!scoreMatch.Success)
                        {
                            scoreMatch = Regex.Match(sub, @"\b(\d{1,3})\b");
                        }
                        if (scoreMatch.Success)
                        {
                            if (decimal.TryParse(scoreMatch.Groups[1].Value, out var scoreVal))
                            {
                                section.ActualScore = scoreVal;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Return default model structure if anything fails
            }

            return audit;
        }

        public static List<ParsedPdfRow> ExtractRowsFromPdf(string pdfPath)
        {
            var rows = new List<ParsedPdfRow>();
            if (!File.Exists(pdfPath))
                return rows;

            try
            {
                string text = "";
                using (var document = PdfDocument.Open(pdfPath))
                {
                    var pagesText = new List<string>();
                    for (int i = 1; i <= document.NumberOfPages; i++)
                    {
                        var page = document.GetPage(i);
                        pagesText.Add(page.Text);
                    }
                    text = string.Join("\n", pagesText);
                }

                if (string.IsNullOrWhiteSpace(text))
                    return rows;

                // Match globally on the entire text block to handle missing line breaks
                var matches = Regex.Matches(text, @"([A-Za-z\s&()\-/\\]+?)(\d{2,6}?)((?:[1-9]\d{0,2}|0)\.\d{2}%|PASS|FAIL)", RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var rawName = match.Groups[1].Value.Trim();
                    var name = CleanParsedName(rawName);

                    if (IsIgnoredName(name)) continue;
                    if (string.IsNullOrWhiteSpace(name) || name.Length < 3) continue;

                    var digits = match.Groups[2].Value;
                    if (digits.Length % 2 == 0)
                    {
                        int half = digits.Length / 2;
                        var part1 = digits.Substring(0, half);
                        var part2 = digits.Substring(half);

                        if (int.TryParse(part1, out var possible) &&
                            int.TryParse(part2, out var achieved))
                        {
                            rows.Add(new ParsedPdfRow
                            {
                                PdfCategoryName = name,
                                PossibleScore = possible,
                                AchievedScore = achieved
                            });
                        }
                    }
                }
            }
            catch
            {
                // Return empty list on error
            }

            return rows;
        }

        private static string CleanParsedName(string name)
        {
            var cleaned = name.Trim();
            
            string[] prefixesToRemove = { 
                "POSSIBLEACHIEVEDPERCENTAGE",
                "PERCENTAGE",
                "ACHIEVED",
                "POSSIBLE",
                "AN ANALYSIS OF THE RATING OBTAINED INDICATES THE FOLLOWING:",
                "FOLLOWING:"
            };

            foreach (var prefix in prefixesToRemove)
            {
                if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(prefix.Length).Trim();
                }
            }

            cleaned = Regex.Replace(cleaned, @"^[^A-Za-z(]+", "").Trim();
            return cleaned;
        }

        private static bool IsIgnoredName(string name)
        {
            return name.Contains("TOTAL", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("SUMMARY", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("SECTION", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Inspection", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("FOLLOWING:", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class ParsedPdfRow
    {
        public string PdfCategoryName { get; set; } = string.Empty;
        public int PossibleScore { get; set; }
        public int AchievedScore { get; set; }
    }
}
