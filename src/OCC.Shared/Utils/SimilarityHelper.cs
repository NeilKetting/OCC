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

            int distance = GetLevenshteinDistance(source.ToLowerInvariant(), target.ToLowerInvariant());
            int maxLength = Math.Max(source.Length, target.Length);

            return 1.0 - ((double)distance / maxLength);
        }
    }
}
