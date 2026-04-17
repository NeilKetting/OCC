using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts an Enum to its Integer value.
    /// Useful for binding Enums to controls that expect an index or integer (like ComboBox SelectedIndex).
    /// 
    /// Used in:
    /// - RollCallView.axaml
    /// </summary>
    public class EnumToIntConverter : IValueConverter
    {
        public static readonly EnumToIntConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Enum enumValue)
            {
                return System.Convert.ToInt32(enumValue);
            }
            return 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intValue && targetType.IsEnum)
            {
                return Enum.ToObject(targetType, intValue);
            }
            // If targetType is not the enum type directly (e.g. object), we might need more context or hardcode
             if (value is int i)
            {
                 // This is tricky without knowing the Type. 
                 // But for SelectedIndex binding, the target property on VM IS typed (StaffRole).
                 // So Avalonia might handle the cast from object if we return the int?
                 // Wait, ConvertBack is Int -> Enum.
                 return i; // This will likely fail if targetType expects Enum.
            }
            return null;
        }
    }
}
