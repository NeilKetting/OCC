using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class UserColorConverter : IValueConverter
    {
        private static readonly string[] Colors = {
            "#0ea5e9", // Sky 500
            "#10b981", // Emerald 500
            "#f59e0b", // Amber 500
            "#ef4444", // Red 500
            "#8b5cf6", // Violet 500
            "#ec4899", // Pink 500
            "#f97316", // Orange 500
            "#6366f1", // Indigo 500
            "#14b8a6", // Teal 500
            "#06b6d4", // Cyan 500
            "#84cc16", // Lime 500
            "#d946ef"  // Fuchsia 500
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Guid? id = null;

            if (value is Guid guid)
            {
                id = guid;
            }
            else if (value is string str && Guid.TryParse(str, out var parsed))
            {
                id = parsed;
            }

            if (id.HasValue)
            {
                var bytes = id.Value.ToByteArray();
                int hash = 17;
                foreach (var b in bytes)
                {
                    hash = hash * 31 + b;
                }
                
                var colorString = Colors[Math.Abs(hash) % Colors.Length];
                return new BrushConverter().ConvertFrom(colorString) as SolidColorBrush ?? Brushes.SkyBlue;
            }

            // Fallback for names or nulls
            if (value != null)
            {
                int hash = (value.ToString() ?? string.Empty).GetHashCode();
                var colorString = Colors[Math.Abs(hash) % Colors.Length];
                return new BrushConverter().ConvertFrom(colorString) as SolidColorBrush ?? Brushes.SkyBlue;
            }

            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
