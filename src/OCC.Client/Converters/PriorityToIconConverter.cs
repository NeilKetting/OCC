using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class PriorityToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string priority && Application.Current != null)
            {
                var resourceKey = priority.ToLower() switch
                {
                    "critical" => "IconFlame",
                    "very high" => "IconArrowUp",
                    "high" => "IconTriangleUp",
                    "medium" => "IconMinus",
                    "low" => "IconTriangleDown",
                    "very low" => "IconArrowDown",
                    _ => "IconX" // Default/None
                };

                if (Application.Current.TryGetResource(resourceKey, out var res) && res is StreamGeometry geometry)
                {
                    return geometry;
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
