using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class DueDateToDisplayConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                if (date.Date == DateTime.Today)
                {
                    return "Due: Today";
                }
                
                // If it's tomorrow
                if (date.Date == DateTime.Today.AddDays(1))
                {
                    return "Due: Tomorrow";
                }

                // If it was yesterday (overdue)
                if (date.Date == DateTime.Today.AddDays(-1))
                {
                    return "Due: Yesterday";
                }

                return $"Due: {date:MMM dd}";
            }
            return "Due: N/A";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
