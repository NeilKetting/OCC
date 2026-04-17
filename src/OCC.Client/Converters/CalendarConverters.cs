using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a boolean (IsCurrentMonth) to a background brush.
    /// Used to distinguish active days from inactive days in the calendar.
    /// 
    /// Used in:
    /// - CalendarView.axaml
    /// </summary>
    public class BoolToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isCurrentMonth && isCurrentMonth)
            {
                // Active Days: White Background (High contrast)
                return Brushes.White;
            }
            // Inactive Days: Slate/Gray Background (Dimmed)
            return new SolidColorBrush(Color.Parse("#F8FAFC"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a boolean (IsCurrentMonth) to Opacity.
    /// 1.0 for current month days, 0.5 for inactive days.
    /// 
    /// Used in:
    /// - CalendarView.axaml
    /// </summary>
    public class BoolToOpacityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isCurrentMonth && isCurrentMonth)
            {
                return 1.0;
            }
            return 0.5; // Dimmed text for inactive days
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
