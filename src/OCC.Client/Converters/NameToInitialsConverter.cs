using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Linq;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a full name (string) to initials (maximum 2 characters).
    /// "John Doe" -> "JD", "Neil" -> "NE".
    /// 
    /// Used in:
    /// - Shared/TopBarView.axaml (User avatar)
    /// - UserManagementView.axaml (User avatar)
    /// </summary>
    public class NameToInitialsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string name && !string.IsNullOrWhiteSpace(name))
            {
                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    return parts[0].Length >= 2 
                        ? parts[0].Substring(0, 2).ToUpper() 
                        : parts[0].ToUpper();
                }
                
                return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
            }
            return "U";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
