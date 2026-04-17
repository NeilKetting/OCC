using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a TimeSpan to its TotalHours (double).
    /// Used for displaying duration in numeric hours.
    /// 
    /// Used in:
    /// - TaskListView.axaml
    /// </summary>
    public class TimeSpanToHoursConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TimeSpan ts)
            {
                return ts.TotalHours;
            }
            return 0d;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
