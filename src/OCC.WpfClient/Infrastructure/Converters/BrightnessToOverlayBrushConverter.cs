using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class BrightnessToOverlayBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double brightness)
            {
                // brightness is 0 to 1, where 0.5 is neutral
                if (brightness == 0.5)
                {
                    return Brushes.Transparent;
                }

                if (brightness > 0.5)
                {
                    // Lighten: White overlay
                    // 0.5 -> 0% opacity
                    // 1.0 -> 40% opacity (capped for usability)
                    double opacity = (brightness - 0.5) * 0.8; 
                    return new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 255, 255, 255));
                }
                else
                {
                    // Darken: Black overlay
                    // 0.5 -> 0% opacity
                    // 0.0 -> 60% opacity
                    double opacity = (0.5 - brightness) * 1.2;
                    return new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0, 0, 0));
                }
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
