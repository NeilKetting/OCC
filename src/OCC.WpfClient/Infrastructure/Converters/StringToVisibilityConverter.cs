using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && parameter is string p)
            {
                // Hide if the value matches the parameter (e.g. don't show "Main" header)
                return s.Equals(p, StringComparison.OrdinalIgnoreCase) ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
