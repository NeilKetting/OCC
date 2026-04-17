using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class FriendlyEnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            string? enumString = value?.ToString();
            if (string.IsNullOrEmpty(enumString)) return string.Empty;

            // Split by capital letters: "SiteManager" -> "Site Manager"
            return Regex.Replace(enumString, "([a-z])([A-Z])", "$1 $2");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
