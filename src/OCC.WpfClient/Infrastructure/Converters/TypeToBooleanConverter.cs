using System;
using System.Globalization;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class TypeToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            
            var valueType = value.GetType();
            if (parameter is Type paramType)
            {
                return valueType == paramType;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is Type targetViewType)
            {
                return targetViewType;
            }
            return Binding.DoNothing;
        }
    }
}
