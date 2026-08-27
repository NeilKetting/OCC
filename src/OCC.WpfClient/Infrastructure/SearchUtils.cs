using System;
using System.Collections.Generic;
using System.Linq;

namespace OCC.WpfClient.Infrastructure
{
    /// <summary>
    /// Utility class providing uniform, multi-token space-delimited search matching across all WPF client list views.
    /// Supports queries like "Lucky M" matching FirstName = "Lucky" and LastName = "Makubule".
    /// </summary>
    public static class SearchUtils
    {
        /// <summary>
        /// Performs multi-token, space-delimited search matching.
        /// Returns true if EVERY space-separated token in <paramref name="searchQuery"/>
        /// matches at least one of the provided target string fields (or composite text).
        /// </summary>
        public static bool MatchesQuery(string? searchQuery, params string?[] targetFields)
        {
            if (string.IsNullOrWhiteSpace(searchQuery)) return true;

            var tokens = searchQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return true;

            var validFields = targetFields.Where(f => !string.IsNullOrEmpty(f)).ToList();
            if (validFields.Count == 0) return false;

            var compositeText = string.Join(" ", validFields);

            return tokens.All(token => compositeText.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Convenience overload for checking a single combined string.
        /// </summary>
        public static bool MatchesQuery(string? searchQuery, string? combinedText)
        {
            if (string.IsNullOrWhiteSpace(searchQuery)) return true;
            if (string.IsNullOrEmpty(combinedText)) return false;

            var tokens = searchQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return tokens.All(token => combinedText.Contains(token, StringComparison.OrdinalIgnoreCase));
        }
    }
}
