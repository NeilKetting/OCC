using System;

namespace OCC.Shared.Utils
{
    public static class SimilarityHelper
    {
        /// <summary>
        /// Calculates the Levenshtein distance between two strings.
        /// Lower distance means higher similarity.
        /// </summary>
        public static int GetLevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 0 : target.Length;
            if (string.IsNullOrEmpty(target)) return source.Length;

            var n = source.Length;
            var m = target.Length;
            var d = new int[n + 1, m + 1];

            for (var i = 0; i <= n; i++) d[i, 0] = i;
            for (var j = 0; j <= m; j++) d[0, j] = j;

            for (var i = 1; i <= n; i++)
            {
                for (var j = 1; j <= m; j++)
                {
                    var cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        /// <summary>
        /// Calculates a similarity score between 0 and 1.
        /// 1 is perfect match, 0 is no similarity.
        /// </summary>
        public static double GetSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target) ? 1.0 : 0.0;

            source = source.ToLowerInvariant().Trim();
            target = target.ToLowerInvariant().Trim();

            if (source == target) return 1.0;

            // 1. Check for Acronyms (e.g. "OCC" vs "Orange Circle Construction")
            if (IsAcronymMatch(source, target)) return 0.95;

            // 1.1 Specialized Internal Matching for "Circle Construction" or "OCC"
            if ((source.Contains("circle") || source == "occ") && (target.Contains("circle") || target.Contains("orange circle")))
                return 0.85;

            // 2. Levenshtein Distance
            int distance = GetLevenshteinDistance(source, target);
            int maxLength = Math.Max(source.Length, target.Length);
            double levScore = 1.0 - ((double)distance / maxLength);

            // 3. Word-based overlap (handles "Circle Construction" vs "Circle Construction - Jhb")
            var sourceWords = source.Split(new[] { ' ', '-', '/', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var targetWords = target.Split(new[] { ' ', '-', '/', '_' }, StringSplitOptions.RemoveEmptyEntries);
            
            int matches = 0;
            foreach (var sw in sourceWords)
            {
                if (sw.Length <= 2) continue; // Skip small words like "of", "in"
                foreach (var tw in targetWords)
                {
                    if (sw == tw) { matches++; break; }
                }
            }

            double wordScore = (double)matches / Math.Max(sourceWords.Length, targetWords.Length);

            return Math.Max(levScore, wordScore);
        }

        private static bool IsAcronymMatch(string source, string target)
        {
            if (source.Length < 2 || target.Length < 2) return false;

            // Try source as acronym of target
            if (CheckAcronym(source, target)) return true;
            
            // Try target as acronym of source
            if (CheckAcronym(target, source)) return true;

            return false;
        }

        private static bool CheckAcronym(string acronym, string fullName)
        {
            var words = fullName.Split(new[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < acronym.Length) return false;

            string generated = "";
            foreach (var w in words)
            {
                if (w.Length > 0) generated += w[0];
            }

            return generated.Contains(acronym);
        }
    }
}
