using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OCC.API.Security
{
    /// <summary>
    /// Security utility class for sanitizing user inputs and validating file operations to prevent OWASP vulnerabilities (XSS, Path Traversal).
    /// </summary>
    public static class InputSanitizer
    {
        private static readonly Regex ScriptBlockRegex = new Regex(@"<script[^>]*>[\s\S]*?</script>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex HtmlTagRegex = new Regex(@"<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex DangerousScriptRegex = new Regex(@"(javascript:|vbscript:|onload=|onerror=|onclick=)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Sanitizes text input by trimming and removing potential HTML/script tags to prevent XSS attacks.
        /// </summary>
        /// <param name="input">The raw input string.</param>
        /// <returns>The sanitized string.</returns>
        public static string Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var sanitized = input.Trim();
            // Remove full script blocks first (e.g. <script>alert(1)</script>)
            sanitized = ScriptBlockRegex.Replace(sanitized, string.Empty);
            // Remove remaining HTML tags
            sanitized = HtmlTagRegex.Replace(sanitized, string.Empty);
            // Remove dangerous event attributes/protocols
            sanitized = DangerousScriptRegex.Replace(sanitized, string.Empty);
            return sanitized.Trim();
        }

        /// <summary>
        /// Validates that a filename does not contain path traversal vectors or invalid path characters.
        /// </summary>
        /// <param name="fileName">The filename to validate.</param>
        /// <returns>True if safe, otherwise false.</returns>
        public static bool IsSafeFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var name = Path.GetFileName(fileName);
            if (name != fileName)
                return false;

            if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
                return false;

            var invalidChars = Path.GetInvalidFileNameChars();
            return !fileName.Any(c => invalidChars.Contains(c));
        }

        /// <summary>
        /// Validates whether a file extension matches an allowed whitelist.
        /// </summary>
        /// <param name="fileName">The filename or path.</param>
        /// <param name="allowedExtensions">Collection of permitted extensions (e.g. ".jpg", ".png").</param>
        /// <returns>True if allowed, otherwise false.</returns>
        public static bool IsAllowedExtension(string fileName, IEnumerable<string> allowedExtensions)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                return false;

            return allowedExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}
