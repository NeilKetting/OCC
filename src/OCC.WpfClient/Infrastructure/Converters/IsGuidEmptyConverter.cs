using System;
using System.Globalization;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class IsGuidEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Guid guid)
            {
                return guid == Guid.Empty;
            }
            return true; // Default to true if not a valid guid (treat as new)
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
