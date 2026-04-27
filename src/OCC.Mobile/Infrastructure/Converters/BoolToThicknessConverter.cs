using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace OCC.Mobile.Infrastructure.Converters
{
    public class BoolToThicknessConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var p = parameter?.ToString()?.Split('|');
            if (p?.Length == 2 && value is bool b)
            {
                return b ? Thickness.Parse(p[0]) : Thickness.Parse(p[1]);
            }
            return new Thickness(0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
