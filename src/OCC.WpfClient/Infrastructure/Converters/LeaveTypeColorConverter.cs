using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OCC.Shared.Models;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class LeaveTypeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LeaveType leaveType)
            {
                return leaveType switch
                {
                    LeaveType.Annual => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3")), // Blue
                    LeaveType.Sick => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")), // Orange
                    LeaveType.FamilyResponsibility => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C27B0")), // Purple
                    LeaveType.Study => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#009688")), // Teal
                    LeaveType.Maternity => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E91E63")), // Pink
                    LeaveType.Unpaid => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E")), // Gray
                    LeaveType.AbsentWithoutLeave => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")), // Red
                    LeaveType.CulturalObligations => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F51B5")), // Indigo
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
