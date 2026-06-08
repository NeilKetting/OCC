using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OCC.Shared.Models;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class LeaveStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LeaveStatus status)
            {
                return status switch
                {
                    LeaveStatus.Pending => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")), // Orange
                    LeaveStatus.Approved => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")), // Green
                    LeaveStatus.Rejected => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")), // Red
                    LeaveStatus.Cancelled => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E")), // Gray
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
