using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OCC.Mobile.Infrastructure.Converters
{
    public class BoolToFontSizeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var p = parameter?.ToString()?.Split('|');
            if (p?.Length == 2 && value is bool b)
            {
                return b ? double.Parse(p[0]) : double.Parse(p[1]);
            }
            return 16.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var p = parameter?.ToString()?.Split('|');
            if (p?.Length == 2 && value is bool b)
            {
                var weightStr = b ? p[0] : p[1];
                return weightStr.Equals("Bold", StringComparison.OrdinalIgnoreCase) ? Avalonia.Media.FontWeight.Bold :
                       weightStr.Equals("SemiBold", StringComparison.OrdinalIgnoreCase) ? Avalonia.Media.FontWeight.SemiBold :
                       Avalonia.Media.FontWeight.Normal;
            }
            return Avalonia.Media.FontWeight.Normal;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
