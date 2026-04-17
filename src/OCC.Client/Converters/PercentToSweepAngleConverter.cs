using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a percentage (0-100) to a sweep angle (0-360) for circular geometry.
    /// Multiplier is 3.6.
    /// 
    /// Used in:
    /// - TimeEfficiencyGraphView.axaml (Donut charts)
    /// </summary>
    public class PercentToSweepAngleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int percent)
            {
                return percent * 3.6;
            }
            if (value is double dPercent)
            {
                return dPercent * 3.6;
            }
            return 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
