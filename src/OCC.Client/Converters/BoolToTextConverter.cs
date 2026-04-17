using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class BoolToTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && parameter is string options)
            {
                var parts = options.Split('|');
                if (parts.Length == 2)
                {
                    return b ? parts[0] : parts[1];
                }
            }
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
