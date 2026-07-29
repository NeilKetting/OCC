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

                // 1. Try inline / tabular pattern: e.g., "Category Name 100 85 85%" or "Category Name 85%"
                var matches = Regex.Matches(text, @"([A-Za-z\s&()\-/\\]+?)(?:(\d{1,3})\s+(\d{1,3}))?\s*(\d{1,3}(?:\.\d{1,2})?)\s*%|(?:PASS|FAIL)", RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var rawName = match.Groups[1].Value.Trim();
                    var name = CleanParsedName(rawName);

                    if (IsIgnoredName(name) || string.IsNullOrWhiteSpace(name) || name.Length < 3) continue;

                    int possible = 100;
                    int achieved = 0;

                    if (!string.IsNullOrEmpty(match.Groups[2].Value) && !string.IsNullOrEmpty(match.Groups[3].Value))
                    {
                        int.TryParse(match.Groups[2].Value, out possible);
                        int.TryParse(match.Groups[3].Value, out achieved);
                    }
                    else if (!string.IsNullOrEmpty(match.Groups[4].Value))
                    {
                        if (double.TryParse(match.Groups[4].Value, out var pct))
                        {
                            achieved = (int)Math.Round(pct);
                        }
                    }

                    if (!rows.Any(r => r.PdfCategoryName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        rows.Add(new ParsedPdfRow
                        {
                            PdfCategoryName = name,
                            PossibleScore = possible,
                            AchievedScore = achieved
                        });
                    }
                }

                // 2. If tabular matching produced fewer than 3 categories, extract standard HSEQ categories and sequence of percentages
                var standardCats = new[]
                {
                    "Administrative Requirements", "Education Training & Promotion", "Public Safety",
                    "Personal Protective Equipment (PPE)", "Housekeeping", "Elevated Work", "Electricity",
                    "Fire Prevention and Protection", "Equipment", "Construction Vehicles and Mobile Plant",
                    "Facilities"
                };

                if (rows.Count < 3)
                {
                    // Extract percentage values
                    var rawPcts = Regex.Matches(text, @"\b(\d{1,3})\s*%")
                                      .Cast<Match>()
                                      .Select(m => int.TryParse(m.Groups[1].Value, out var v) ? v : -1)
                                      .Where(v => v >= 0 && v <= 100)
                                      .ToList();

                    // Filter out Y-axis scale sequences (0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100)
                    var scorePcts = new List<int>();
                    for (int i = 0; i < rawPcts.Count; i++)
                    {
                        if (i <= rawPcts.Count - 11 &&
                            rawPcts[i] == 0 && rawPcts[i + 1] == 10 && rawPcts[i + 2] == 20 &&
                            rawPcts[i + 3] == 30 && rawPcts[i + 4] == 40 && rawPcts[i + 5] == 50)
                        {
                            i += 10; // Skip 0% to 100% axis scale ticks
                            continue;
                        }
                        scorePcts.Add(rawPcts[i]);
                    }

                    int pctIdx = 0;
                    foreach (var cat in standardCats)
                    {
                        if (text.IndexOf(cat, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            int score = (pctIdx < scorePcts.Count) ? scorePcts[pctIdx++] : 0;
                            rows.Add(new ParsedPdfRow
                            {
                                PdfCategoryName = cat,
                                PossibleScore = 100,
                                AchievedScore = score
                            });
                        }
                    }
                }

                // 3. Fallback: If still empty, supply standard categories so screen is never blank
                if (!rows.Any())
                {
                    foreach (var cat in standardCats)
                    {
                        rows.Add(new ParsedPdfRow
                        {
                            PdfCategoryName = cat,
                            PossibleScore = 100,
                            AchievedScore = 0
                        });
                    }
                }
            }
            catch
            {
                // Fallback on error
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
