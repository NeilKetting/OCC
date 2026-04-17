using Avalonia.Data.Converters;
using Avalonia.Media;
using OCC.Shared.Models;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class EventTypeToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ClockEventType eventType)
            {
                if (eventType == ClockEventType.ClockIn)
                    return new SolidColorBrush(Color.Parse("#28A745")); // Green
                else
                    return new SolidColorBrush(Color.Parse("#DC3545")); // Red
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
