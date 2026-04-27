using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace OCC.Mobile.Infrastructure.Converters
{
    public class IndentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int indentLevel)
            {
                // Each indent level is 20 units
                return new Thickness(indentLevel * 20, 0, 0, 15);
            }
            return new Thickness(0, 0, 0, 15);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
