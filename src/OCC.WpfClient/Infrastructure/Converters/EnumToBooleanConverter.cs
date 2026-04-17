using System;
using System.Globalization;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;

            string checkValue = parameter.ToString() ?? string.Empty;
            string targetValue = value.ToString() ?? string.Empty;

            return string.Equals(checkValue, targetValue, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
            {
                var paramString = parameter.ToString();
                if (!string.IsNullOrEmpty(paramString))
                {
                    return Enum.Parse(targetType, paramString);
                }
            }

            return Binding.DoNothing;
        }
    }
}
