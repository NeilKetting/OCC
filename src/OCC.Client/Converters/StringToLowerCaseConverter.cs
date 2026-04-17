using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a string to Lower Case (e.g., "John.Doe@Example.com" -> "john.doe@example.com").
    /// </summary>
    public class StringToLowerCaseConverter : IValueConverter
    {
        public static readonly StringToLowerCaseConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                return text.ToLowerInvariant();
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
