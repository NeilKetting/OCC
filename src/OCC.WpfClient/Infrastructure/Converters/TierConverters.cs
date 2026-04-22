using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class TierToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var tier = value?.ToString() ?? "Silver";
            var resourceName = tier switch
            {
                "Diamond" => "IconDiamond",
                "Gold" => "IconMedal",
                "Silver" => "IconMedal",
                "Bronze" => "IconMedal",
                _ => "IconMedal"
            };

            return Application.Current.TryFindResource(resourceName) ?? Geometry.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class TierToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var tier = value?.ToString() ?? "Silver";
            return tier switch
            {
                "Diamond" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B9F2FF")),
                "Gold" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")),
                "Silver" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0C0C0")),
                "Bronze" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD7F32")),
                _ => Brushes.White
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
