using System;
using System.Globalization;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class FirstLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrEmpty(s))
            {
                char first = char.ToUpper(s[0]);
                if (char.IsLetter(first))
                    return first.ToString();
                return "#";
            }
            return "#";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
