using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class PercentageToWidthConverter : IMultiValueConverter, IValueConverter
    {
        public static PercentageToWidthConverter Instance { get; } = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 && values[0] is double percentage && values[1] is double totalWidth)
            {
                return (percentage / 100.0) * totalWidth;
            }
            
            if (values.Count >= 2 && values[0] is int intPercentage && values[1] is double tw)
            {
                return (intPercentage / 100.0) * tw;
            }

            return 0.0;
        }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double percentage && parameter is string totalWidthStr && double.TryParse(totalWidthStr, out double totalWidth))
            {
                return (percentage / 100.0) * totalWidth;
            }
            
            if (value is int intPercentage && parameter is string twStr && double.TryParse(twStr, out double tw))
            {
                return (intPercentage / 100.0) * tw;
            }

            return 0.0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
