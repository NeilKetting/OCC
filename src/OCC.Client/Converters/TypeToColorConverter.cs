using Avalonia.Data.Converters;
using Avalonia.Media;
using OCC.Shared.Models;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class TypeToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isDevComment && parameter is string target)
            {
                if (target == "CommentBg")
                {
                    // Dev (Neil): Dark gray (#334155), User: Light blue (#F0F9FF)
                    return isDevComment ? SolidColorBrush.Parse("#334155") : SolidColorBrush.Parse("#F0F9FF");
                }
                else if (target == "CommentFg")
                {
                    // Dev (Neil): White, User: Dark Slate (#1E293B)
                    return isDevComment ? SolidColorBrush.Parse("#FFFFFF") : SolidColorBrush.Parse("#1E293B");
                }
            }

            if (value is BugReportType reportType)
            {
                return reportType switch
                {
                    BugReportType.Bug => SolidColorBrush.Parse("#EF4444"),       // Red
                    BugReportType.Suggestion => SolidColorBrush.Parse("#06B6D4"), // Cyan/Teal
                    BugReportType.Question => SolidColorBrush.Parse("#8B5CF6"),   // Purple
                    _ => SolidColorBrush.Parse("#64748B")                         // Gray (Other)
                };
            }

            return SolidColorBrush.Parse("#64748B");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
