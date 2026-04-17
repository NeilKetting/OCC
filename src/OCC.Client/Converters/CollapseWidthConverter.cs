using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Calculates the sidebar width based on collapsed state.
    /// Returns 70.0 if collapsed, 250.0 if expanded.
    /// </summary>
    public class CollapseWidthConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isCollapsed)
            {
                return isCollapsed ? 70.0 : 250.0;
            }
            return 250.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
