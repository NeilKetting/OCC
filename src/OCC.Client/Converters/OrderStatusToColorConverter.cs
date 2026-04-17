using Avalonia.Data.Converters;
using Avalonia.Media;
using OCC.Shared.Models;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class OrderStatusToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is OrderStatus status)
            {
                // Return different colors based on status
                // If parameter is "Background", return lighter/opacity version
                bool isBackground = parameter as string == "Background";

                return status switch
                {
                    OrderStatus.Ordered => isBackground ? SolidColorBrush.Parse("#103B82F6") : SolidColorBrush.Parse("#3B82F6"), // Blue-500
                    OrderStatus.PartialDelivery => isBackground ? SolidColorBrush.Parse("#10F59E0B") : SolidColorBrush.Parse("#F59E0B"), // Amber-500
                    OrderStatus.Completed => isBackground ? SolidColorBrush.Parse("#1022C55E") : SolidColorBrush.Parse("#22C55E"), // Green-500
                    OrderStatus.Cancelled => isBackground ? SolidColorBrush.Parse("#10EF4444") : SolidColorBrush.Parse("#EF4444"), // Red-500
                    _ => isBackground ? SolidColorBrush.Parse("#1064748B") : SolidColorBrush.Parse("#64748B") // Slate-500
                };
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
