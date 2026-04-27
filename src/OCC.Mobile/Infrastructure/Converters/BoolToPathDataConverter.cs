using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OCC.Mobile.Infrastructure.Converters
{
    public class BoolToPathDataConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var p = parameter?.ToString()?.Split('|');
            if (p?.Length == 2 && value is bool b)
            {
                var dataStr = b ? p[0] : p[1];
                return StreamGeometry.Parse(dataStr);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
