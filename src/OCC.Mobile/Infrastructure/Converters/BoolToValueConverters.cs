using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OCC.Mobile.Infrastructure.Converters
{
    public class BoolToFontSizeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var p = parameter?.ToString()?.Split('|');
            if (p?.Length == 2 && value is bool b)
            {
                return b ? double.Parse(p[0]) : double.Parse(p[1]);
            }
            return 16.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var p = parameter?.ToString()?.Split('|');
            if (p?.Length == 2 && value is bool b)
            {
                var weightStr = b ? p[0] : p[1];
                return weightStr.Equals("Bold", StringComparison.OrdinalIgnoreCase) ? Avalonia.Media.FontWeight.Bold :
                       weightStr.Equals("SemiBold", StringComparison.OrdinalIgnoreCase) ? Avalonia.Media.FontWeight.SemiBold :
                       Avalonia.Media.FontWeight.Normal;
            }
            return Avalonia.Media.FontWeight.Normal;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var p = parameter?.ToString()?.Split('|');
            if (p?.Length == 2 && value is bool b)
            {
                return Avalonia.Media.Brush.Parse(b ? p[0] : p[1]);
            }
            return Avalonia.Media.Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var status = value?.ToString();
            if (string.IsNullOrEmpty(status))
            {
                status = "Not Started";
            }

            // Map status string to the hex color from Figma theme.css
            string hexColor = status switch
            {
                "Completed" or "Done" => "#10B981",    // Emerald green
                "Almost Done" => "#2DD4BF",            // Teal
                "Halfway" => "#22D3EE",                // Sky blue
                "Started" or "In Progress" => "#6366F1", // Indigo / Primary
                "On Hold" => "#F59E0B",                // Amber
                "Overdue" => "#EF4444",                // Red (Destructive)
                "Not Started" or "To Do" => "#6B77A4", // Muted slate
                _ => "#6B77A4"
            };

            var color = Color.Parse(hexColor);
            var mode = parameter?.ToString();

            if (mode == "Bg")
            {
                return new SolidColorBrush(color, 0.10); // 10% opacity
            }
            else if (mode == "Border")
            {
                return new SolidColorBrush(color, 0.40); // 40% opacity
            }

            return new SolidColorBrush(color); // 100% opacity
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
