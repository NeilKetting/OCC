using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                if (status.Equals("Absent", StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(Color.Parse("#FFF1F2")); // Light Red
                }
                if (status.Equals("Late", StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(Color.Parse("#FFFBEB")); // Light Yellow/Orange
                }
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
