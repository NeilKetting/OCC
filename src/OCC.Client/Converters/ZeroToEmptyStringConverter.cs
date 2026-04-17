using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OCC.Client.Converters
{
    public class ZeroToEmptyStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int i && i == 0) return string.Empty;
            if (value is double d && Math.Abs(d) < 0.0001) return string.Empty;
            if (value is decimal m && m == 0m) return string.Empty;
            if (value is float f && Math.Abs(f) < 0.0001) return string.Empty;
            
            // Format if parameter is provided, else default string
            if (parameter is string format && value is IFormattable formattable)
            {
                return formattable.ToString(format, culture);
            }

            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(value as string))
            {
                if (targetType == typeof(int)) return 0;
                if (targetType == typeof(double)) return 0.0;
                if (targetType == typeof(decimal)) return 0m;
                if (targetType == typeof(float)) return 0f;
            }

            try 
            {
                // Smart Input: Handle both dot and comma as decimal separators
                if (value is string s && (targetType == typeof(double) || targetType == typeof(decimal) || targetType == typeof(float)))
                {
                    // Normalize to dot for ChangeType
                    var normalized = s.Replace(',', '.');
                    return System.Convert.ChangeType(normalized, targetType, CultureInfo.InvariantCulture);
                }

                return System.Convert.ChangeType(value, targetType, culture);
            }
            catch
            {
                 // Fallback
                 if (targetType == typeof(int)) return 0;
                 if (targetType == typeof(decimal)) return 0m;
                 if (targetType == typeof(double)) return 0.0;
                 return 0;
            }
        }
    }
}
