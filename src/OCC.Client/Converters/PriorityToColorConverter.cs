using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a Priority string (Critical, High, Medium, Low) to a corresponding Brush color.
    /// used to color-code task priorities.
    /// 
    /// Used in:
    /// - TaskListView.axaml
    /// </summary>
    public class PriorityToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string priority)
            {
                // Normalize case
                switch (priority.ToLower())
                {
                    case "critical":
                        return Brush.Parse("#EF4444"); // Red-500
                    case "very high":
                        return Brush.Parse("#F97316"); // Orange-500
                    case "high":
                        return Brush.Parse("#F59E0B"); // Amber-500
                    case "medium":
                        return Brush.Parse("#3B82F6"); // Blue-500
                    case "low":
                        return Brush.Parse("#10B981"); // Emerald-500
                    case "very low":
                        return Brush.Parse("#64748B"); // Slate-500
                    default:
                        return Brush.Parse("#94A3B8"); // Slate-400
                }
            }
            return Brush.Parse("#94A3B8");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
