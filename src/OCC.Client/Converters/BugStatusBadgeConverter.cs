using Avalonia.Data.Converters;
using Avalonia.Media;
using OCC.Shared.Models;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class BugStatusBadgeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is BugReport bug)
            {
                string param = parameter as string ?? "Text";

                if (param == "Text")
                {
                    return bug.Status == "Open" ? bug.Type.ToString() : bug.Status;
                }
                
                if (param == "Background")
                {
                    // If not open, use status color
                    if (bug.Status != "Open")
                    {
                        return bug.Status switch
                        {
                            "In Progress" => Brushes.Orange,
                            "Planning" => Brushes.SlateGray,
                            "Feature Update" => Brushes.DeepSkyBlue,
                            "Fixed" => Brushes.MediumSeaGreen, // Use Green for Fixed to make it clear
                            "Waiting for Client" => Brushes.Goldenrod,
                            "Resolved" => Brushes.MediumSeaGreen,
                            "Closed" => Brushes.LightGray,
                            _ => Brushes.SlateGray
                        };
                    }

                    // If open, use type color
                    return bug.Type switch
                    {
                        BugReportType.Bug => SolidColorBrush.Parse("#EF4444"),       // Red
                        BugReportType.Suggestion => SolidColorBrush.Parse("#06B6D4"), // Cyan/Teal
                        BugReportType.Question => SolidColorBrush.Parse("#8B5CF6"),   // Purple
                        _ => SolidColorBrush.Parse("#64748B")                         // Gray
                    };
                }
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
