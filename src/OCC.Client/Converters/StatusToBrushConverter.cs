using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a status string (Green, Gray, Red, Orange) to a SolidColorBrush.
    /// Used for status indicators columns in lists.
    /// 
    /// Used in:
    /// - ProjectListView.axaml
    /// - TaskListView.axaml
    /// </summary>
    public class StatusToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                // Colors matching the design
                try
                {
                    return status.ToLower() switch
                    {
                        "green" => SolidColorBrush.Parse("#22C55E"), // Green-500
                        "gray" => SolidColorBrush.Parse("#94A3B8"),  // Slate-400
                        "red" => SolidColorBrush.Parse("#EF4444"),   // Red-500
                        "orange" => SolidColorBrush.Parse("#F97316"),// Orange-500
                        _ => SolidColorBrush.Parse(status) // Try to parse as Hex
                    };
                }
                catch
                {
                    return Brushes.Transparent;
                }
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
