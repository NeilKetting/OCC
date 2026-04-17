using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Checks if a string (Value) contains a substring (Parameter).
    /// Used for filtering visibility based on string matches.
    /// 
    /// Used in:
    /// - UserManagementView.axaml (Filter Logic)
    /// </summary>
    public class StringContainsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str && parameter is string substring)
            {
                return str.Contains(substring, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
