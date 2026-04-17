using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Checks if a string (Value) matches another string (Parameter).
    /// Used for toggle buttons or tab selection state.
    /// 
    /// Used in:
    /// - UserManagementView.axaml
    /// </summary>
    public class StringEqualityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value?.ToString() is string val && parameter?.ToString() is string param)
            {
                return val.Equals(param, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
