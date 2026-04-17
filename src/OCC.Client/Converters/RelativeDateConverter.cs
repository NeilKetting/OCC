using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a DateTime to a relative human-readable string.
    /// Returns "Today", "Yesterday", "Tomorrow", or formatted date "MMM d".
    /// 
    /// Used in:
    /// - TaskDetailView.axaml
    /// </summary>
    public class RelativeDateConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            DateTime? dt = null;
            if (value is DateTime dateTime)
                dt = dateTime;
            else if (value is DateTimeOffset dto)
                dt = dto.DateTime;

            if (!dt.HasValue)
                return "None";

            var date = dt.Value.Date;
            var today = DateTime.Today;

            if (date == today)
                return "Today";
            if (date == today.AddDays(-1))
                return "Yesterday";
            if (date == today.AddDays(1))
                return "Tomorrow";

            return date.ToString("MMM d");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a TimeSpan or double (hours) to a duration string (e.g., "5 days").
    /// 
    /// Used in:
    /// - TaskDetailView.axaml (Gantt duration display)
    /// </summary>
    public class DurationConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TimeSpan ts)
            {
                int days = (int)Math.Ceiling(ts.TotalDays);
                if (days <= 0) return "None";
                return $"{days} {(days == 1 ? "day" : "days")}";
            }
            if (value is double hours)
            {
                int days = (int)Math.Ceiling(hours / 24.0);
                if (days <= 0 && hours > 0) return "1 day"; // Minimum 1 day if any hours
                if (days <= 0) return "None";
                return $"{days} {(days == 1 ? "day" : "days")}";
            }
            return value?.ToString() ?? "None";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
