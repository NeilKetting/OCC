using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OCC.WpfClient.Infrastructure.Converters
{
    /// <summary>
    /// Converts a DateTime? expiry date into a color brush indicating status:
    /// Red for expired (&lt; 0 days), Amber for expiring soon (&lt;= 30 days), Green for valid (&gt; 30 days).
    /// </summary>
    public class ExpiryColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DateTime? date = null;

            if (value is DateTime dt)
            {
                date = dt;
            }
            else if (value is string dateStr && DateTime.TryParse(dateStr, out var parsed))
            {
                date = parsed;
            }

            if (!date.HasValue)
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")); // Gray/Slate
            }

            var daysLeft = (date.Value.Date - DateTime.Today).Days;

            if (daysLeft < 0)
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B")); // Red (Expired)
            }
            if (daysLeft <= 30)
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E")); // Amber (Expiring Soon)
            }

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#065F46")); // Green (Valid)
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
