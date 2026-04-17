using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a string to Title Case (e.g., "JOHN DOE" -> "John Doe").
    /// </summary>
    public class StringToTitleCaseConverter : IValueConverter
    {
        public static readonly StringToTitleCaseConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                // Ensure proper casing by lowering first if it's all caps, 
                // though ToTitleCase handles most mixed casing reasonably well, 
                // lowercasing first safeguards against "JOHN DOE" -> "JOHN DOE" (if culture assumes only first letter change).
                // Actually ToTitleCase("JOHN DOE") is "John Doe" usually, but ToTitleCase("JOHN") might be tricky.
                // Best practice: text.ToLower() first.
                return culture.TextInfo.ToTitleCase(text.ToLower());
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
