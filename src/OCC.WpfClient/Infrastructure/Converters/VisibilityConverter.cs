using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = false;

            if (value is bool b)
            {
                isVisible = b;
            }
            else if (value is string s)
            {
                isVisible = !string.IsNullOrWhiteSpace(s);
            }
            else if (value is int i)
            {
                isVisible = i != 0;
            }
            else if (value is long l)
            {
                isVisible = l != 0;
            }
            else if (value is ICollection collection)
            {
                isVisible = collection.Count > 0;
            }
            else if (value is IEnumerable enumerable)
            {
                isVisible = enumerable.Cast<object>().Any();
            }
            else
            {
                isVisible = value != null;
            }

            // Invert logic if parameter contains 'Invert'
            if (parameter?.ToString()?.Contains("Invert", StringComparison.OrdinalIgnoreCase) == true)
            {
                isVisible = !isVisible;
            }

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
