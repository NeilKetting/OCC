using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class EqualityConverter : IValueConverter, IMultiValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEqual;
            if (value == null && parameter == null) isEqual = true;
            else if (value == null || parameter == null) isEqual = false;
            else isEqual = string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);

            if (targetType == typeof(Visibility))
            {
                return isEqual ? Visibility.Visible : Visibility.Collapsed;
            }

            return isEqual;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
            {
                if (targetType == typeof(int) || targetType == typeof(int?))
                {
                    if (int.TryParse(parameter.ToString(), out int intVal)) return intVal;
                }
                return parameter;
            }
            return Binding.DoNothing;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return false;
            
            bool isEqual;
            if (values[0] == null && values[1] == null) isEqual = true;
            else if (values[0] == null || values[1] == null) isEqual = false;
            else isEqual = string.Equals(values[0].ToString(), values[1].ToString(), StringComparison.OrdinalIgnoreCase);

            if (targetType == typeof(Visibility))
            {
                return isEqual ? Visibility.Visible : Visibility.Collapsed;
            }

            return isEqual;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
