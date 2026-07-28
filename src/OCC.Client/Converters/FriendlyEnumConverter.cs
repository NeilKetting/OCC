using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts an Enum Value to a friendly string by splitting keys by capitals.
    /// Example: "SiteManager" -> "Site Manager".
    /// 
    /// Used in:
    /// - UserManagementView.axaml
    /// - EmployeeDetailView.axaml
    /// </summary>
    public class FriendlyEnumConverter : IValueConverter
    {
        public static readonly FriendlyEnumConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return null;

            if (value is Enum enumValue)
            {
                var fi = enumValue.GetType().GetField(enumValue.ToString());
                if (fi != null)
                {
                    var attributes = (System.ComponentModel.DescriptionAttribute[])fi.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
                    if (attributes.Length > 0)
                    {
                        return attributes[0].Description;
                    }
                }
            }

            string name = value.ToString() ?? string.Empty;
            
            // Add spaces before capitals (e.g., SiteManager -> Site Manager)
            return Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return null; // One-way conversion only
        }
    }
}
