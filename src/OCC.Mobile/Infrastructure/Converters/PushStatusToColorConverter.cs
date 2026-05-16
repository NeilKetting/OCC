using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OCC.Mobile.Infrastructure.Converters
{
    public class PushStatusToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var status = value as string;
            if (string.IsNullOrEmpty(status)) return Brushes.Gray;

            if (status.Contains("Success", StringComparison.OrdinalIgnoreCase))
                return Brushes.LimeGreen;
            
            if (status.Contains("Error", StringComparison.OrdinalIgnoreCase) || status.Contains("FAILED", StringComparison.OrdinalIgnoreCase))
                return Brushes.Red;
            
            if (status.Contains("Waiting", StringComparison.OrdinalIgnoreCase))
                return Brushes.Orange;

            return Brushes.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
