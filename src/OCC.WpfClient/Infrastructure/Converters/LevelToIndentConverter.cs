using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class LevelToIndentConverter : IValueConverter
    {
        public double IndentStep { get; set; } = 20.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                return new Thickness(level * IndentStep, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
