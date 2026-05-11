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
            string? s = value?.ToString();
            bool hasValue = !string.IsNullOrWhiteSpace(s);
            bool invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) ?? false;

            if (parameter is string p && !invert)
            {
                // Equality check if parameter is provided and not "Invert"
                return s != null && s.Equals(p, StringComparison.OrdinalIgnoreCase) ? Visibility.Collapsed : Visibility.Visible;
            }

            if (invert)
            {
                return hasValue ? Visibility.Collapsed : Visibility.Visible;
            }

            return hasValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
