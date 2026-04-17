using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a start angle and sweep angle into a StreamGeometry for a donut chart segment.
    /// 
    /// Used in:
    /// - TimeEfficiencyGraphView.axaml
    /// </summary>
    public class DonutSegmentConverter : IMultiValueConverter
    {
        public object? Convert(System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2 || 
                values[0] is not double startAngle || 
                values[1] is not double sweepAngle)
            {
                return null; // Return empty/null geometry
            }

            double size = 100; // Default size, or pass as parameter
            if (parameter is double paramSize) size = paramSize;
            else if (parameter is string paramStr && double.TryParse(paramStr, out double sizes)) size = sizes;

            double thickness = 15; // Stroke thickness
            double radius = (size - thickness) / 2;
            Point center = new Point(size / 2, size / 2);

            if (sweepAngle >= 360)
            {
                 // Full circle (Ring)
                 var geometry = new StreamGeometry();
                 using (var context = geometry.Open())
                 {
                     context.BeginFigure(new Point(center.X + radius, center.Y), false);
                     context.ArcTo(new Point(center.X - radius, center.Y), new Size(radius, radius), 180, false, SweepDirection.Clockwise);
                     context.ArcTo(new Point(center.X + radius, center.Y), new Size(radius, radius), 180, false, SweepDirection.Clockwise);
                 }
                 return geometry;
            }

            Point startPoint = PolarToCartesian(center, radius, startAngle);
            Point endPoint = PolarToCartesian(center, radius, startAngle + sweepAngle);

            bool isLargeArc = sweepAngle > 180.0;
            Size sizeRadius = new Size(radius, radius);

            StreamGeometry geom = new StreamGeometry();
            using (StreamGeometryContext ctx = geom.Open())
            {
                ctx.BeginFigure(startPoint, false);
                ctx.ArcTo(endPoint, sizeRadius, 0, isLargeArc, SweepDirection.Clockwise);
            }

            return geom;
        }

        private Point PolarToCartesian(Point center, double radius, double angleInDegrees)
        {
            // Subtract 90 to start at 12 o'clock
            double angleInRadians = (angleInDegrees - 90) * Math.PI / 180.0;
            return new Point(
                center.X + (radius * Math.Cos(angleInRadians)),
                center.Y + (radius * Math.Sin(angleInRadians)));
        }
    }
}
