using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using Avalonia;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a boolean value to a chevron icon resource.
    /// True returns "IconChevronDown" (Expanded), False returns "IconChevronRight" (Collapsed).
    /// 
    /// Used in:
    /// - TaskListView.axaml (Groups)
    /// - ProjectListView.axaml (Groups)
    /// </summary>
    public class BoolToChevronConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                var key = isExpanded ? "IconChevronDown" : "IconChevronRight";
                if (Application.Current != null && Application.Current.TryGetResource(key, null, out var resource))
                {
                    return resource;
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
