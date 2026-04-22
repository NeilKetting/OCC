using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OCC.Shared.Models;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SnagStatus status)
            {
                return status switch
                {
                    SnagStatus.Open => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252")), // Red
                    SnagStatus.InProgress => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD740")), // Amber
                    SnagStatus.Fixed => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#40C4FF")), // Light Blue
                    SnagStatus.Verified => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#69F0AE")), // Light Green
                    SnagStatus.Closed => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDBDBD")), // Gray
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
