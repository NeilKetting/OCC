using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            
            // If parameter is "Invert", return Visible if NOT null
            if (parameter?.ToString() == "Invert")
            {
                return isNull ? Visibility.Collapsed : Visibility.Visible;
            }

            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
