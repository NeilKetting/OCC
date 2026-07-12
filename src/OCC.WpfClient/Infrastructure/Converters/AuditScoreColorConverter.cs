using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class AuditScoreColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double score)
            {
                return GetColorForScore(score);
            }
            if (value is int scoreInt)
            {
                return GetColorForScore(scoreInt);
            }
            if (value is decimal scoreDec)
            {
                return GetColorForScore((double)scoreDec);
            }
            if (value != null && double.TryParse(value.ToString(), out double parsedScore))
            {
                return GetColorForScore(parsedScore);
            }
            return Brushes.Transparent;
        }

        private Brush GetColorForScore(double score)
        {
            if (score >= 90)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")); // Green
            if (score >= 75)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBC02D")); // Gold/Amber
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828")); // Red
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
