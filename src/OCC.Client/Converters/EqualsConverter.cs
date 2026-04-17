using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OCC.Client.Converters
{
    public class EqualsConverter : IValueConverter, IMultiValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null) return true;
            if (value == null || parameter == null) return false;
            
            return value.Equals(parameter);
        }

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2) return false;
            
            var first = values[0];
            var second = values[1];

            if (first == null && second == null) return true;
            if (first == null || second == null) return false;

            return first.Equals(second);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }
}
