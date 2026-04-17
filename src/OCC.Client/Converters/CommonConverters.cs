using System;
using System.Globalization;
using Avalonia.Data.Converters;
using OCC.Shared.Models;

namespace OCC.Client.Converters
{
    public class GuidToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNotEmpty = false;
            if (value is Guid guid)
            {
                isNotEmpty = guid != Guid.Empty;
            }
            
            if (parameter?.ToString() == "!")
            {
                return !isNotEmpty;
            }
            return isNotEmpty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }

    public class InvertBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }
    }

    public class NullToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNotNull = value != null;
            if (parameter?.ToString() == "!") return !isNotNull;
            return isNotNull;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }

    public class EmployeeToNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Employee employee)
            {
                return employee.DisplayName;
            }
            
            if (value is string s) return s;

            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Employee employee) return employee.DisplayName;
            return value;
        }
    }

    public class DoubleToStarGridLengthConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                // Ensure a minimum non-zero value for 0 stock to avoid grid sizing issues if all are 0
                // though usually the container has a fixed height/alignment.
                return new Avalonia.Controls.GridLength(Math.Max(0.0001, d), Avalonia.Controls.GridUnitType.Star);
            }
            return new Avalonia.Controls.GridLength(0, Avalonia.Controls.GridUnitType.Star);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }

    public class BoolToStarGridLengthConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                return new Avalonia.Controls.GridLength(1, Avalonia.Controls.GridUnitType.Star);
            }
            return new Avalonia.Controls.GridLength(0, Avalonia.Controls.GridUnitType.Pixel);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }
}
