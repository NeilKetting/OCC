using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OCC.Shared.Models;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class OrderStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OrderStatus status)
            {
                return status switch
                {
                    OrderStatus.Draft => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E")), // Gray
                    OrderStatus.Ordered => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3")), // Blue
                    OrderStatus.PartialDelivery => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")), // Orange
                    OrderStatus.Completed => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")), // Green
                    OrderStatus.Finalised => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#009688")), // Teal
                    OrderStatus.Cancelled => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")), // Red
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
