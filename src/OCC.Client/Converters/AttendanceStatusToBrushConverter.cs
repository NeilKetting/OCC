using Avalonia.Data.Converters;
using Avalonia.Media;
using OCC.Shared.Models;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class AttendanceStatusToBrushConverter : IValueConverter
    {
        public static readonly AttendanceStatusToBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is AttendanceStatus status)
            {
                return status switch
                {
                    AttendanceStatus.Present => SolidColorBrush.Parse("#22C55E"), // Green-500
                    AttendanceStatus.Late => SolidColorBrush.Parse("#F59E0B"), // Amber-500
                    AttendanceStatus.Absent => SolidColorBrush.Parse("#EF4444"), // Red-500
                    AttendanceStatus.LeaveEarly => SolidColorBrush.Parse("#EF4444"), // Red-500
                    AttendanceStatus.Sick => SolidColorBrush.Parse("#F97316"), // Orange-500
                    AttendanceStatus.LeaveAuthorized => SolidColorBrush.Parse("#3B82F6"), // Blue-500
                    _ => Brushes.Gray
                };
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
