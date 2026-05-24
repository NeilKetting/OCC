using System.Windows;
using System.Windows.Media;

namespace OCC.WpfClient.Infrastructure.AttachedProperties
{
    public static class ButtonProperties
    {
        // Icon Property (Segoe MDL2 Assets glyph)
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.RegisterAttached(
                "Icon",
                typeof(string),
                typeof(ButtonProperties),
                new FrameworkPropertyMetadata(null));

        public static string GetIcon(DependencyObject d) => (string)d.GetValue(IconProperty);
        public static void SetIcon(DependencyObject d, string value) => d.SetValue(IconProperty, value);

        // IconForeground Property
        public static readonly DependencyProperty IconForegroundProperty =
            DependencyProperty.RegisterAttached(
                "IconForeground",
                typeof(Brush),
                typeof(ButtonProperties),
                new FrameworkPropertyMetadata(Brushes.White));

        public static Brush GetIconForeground(DependencyObject d) => (Brush)d.GetValue(IconForegroundProperty);
        public static void SetIconForeground(DependencyObject d, Brush value) => d.SetValue(IconForegroundProperty, value);

        // IconSize Property
        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.RegisterAttached(
                "IconSize",
                typeof(double),
                typeof(ButtonProperties),
                new FrameworkPropertyMetadata(14.0));

        public static double GetIconSize(DependencyObject d) => (double)d.GetValue(IconSizeProperty);
        public static void SetIconSize(DependencyObject d, double value) => d.SetValue(IconSizeProperty, value);

        // HoverBackground Property
        public static readonly DependencyProperty HoverBackgroundProperty =
            DependencyProperty.RegisterAttached(
                "HoverBackground",
                typeof(Brush),
                typeof(ButtonProperties),
                new FrameworkPropertyMetadata(null));

        public static Brush GetHoverBackground(DependencyObject d) => (Brush)d.GetValue(HoverBackgroundProperty);
        public static void SetHoverBackground(DependencyObject d, Brush value) => d.SetValue(HoverBackgroundProperty, value);

        // HoverBorderBrush Property
        public static readonly DependencyProperty HoverBorderBrushProperty =
            DependencyProperty.RegisterAttached(
                "HoverBorderBrush",
                typeof(Brush),
                typeof(ButtonProperties),
                new FrameworkPropertyMetadata(null));

        public static Brush GetHoverBorderBrush(DependencyObject d) => (Brush)d.GetValue(HoverBorderBrushProperty);
        public static void SetHoverBorderBrush(DependencyObject d, Brush value) => d.SetValue(HoverBorderBrushProperty, value);

        // HoverForeground Property
        public static readonly DependencyProperty HoverForegroundProperty =
            DependencyProperty.RegisterAttached(
                "HoverForeground",
                typeof(Brush),
                typeof(ButtonProperties),
                new FrameworkPropertyMetadata(Brushes.White));

        public static Brush GetHoverForeground(DependencyObject d) => (Brush)d.GetValue(HoverForegroundProperty);
        public static void SetHoverForeground(DependencyObject d, Brush value) => d.SetValue(HoverForegroundProperty, value);

        // HoverIconForeground Property
        public static readonly DependencyProperty HoverIconForegroundProperty =
            DependencyProperty.RegisterAttached(
                "HoverIconForeground",
                typeof(Brush),
                typeof(ButtonProperties),
                new FrameworkPropertyMetadata(null));

        public static Brush GetHoverIconForeground(DependencyObject d) => (Brush)d.GetValue(HoverIconForegroundProperty);
        public static void SetHoverIconForeground(DependencyObject d, Brush value) => d.SetValue(HoverIconForegroundProperty, value);
    }
}
