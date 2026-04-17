using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OCC.Client.Converters
{
    public class MathConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double v && double.TryParse(parameter?.ToString(), NumberStyles.Any, culture, out var p))
            {
                // Simple subtraction: Value - Parameter
                // Used for MaxHeight = WindowHeight - Margin
                return v - p;
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
