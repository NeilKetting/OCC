using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class CollectionContainsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return false;

            var collection = values[0] as IEnumerable;
            var item = values[1];

            if (collection == null || item == null) return false;

            foreach (var existingItem in collection)
            {
                if (existingItem != null && existingItem.Equals(item))
                    return true;
            }

            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
