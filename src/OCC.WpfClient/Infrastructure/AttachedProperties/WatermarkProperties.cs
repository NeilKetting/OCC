using System.Windows;
using System.Windows.Controls;

namespace OCC.WpfClient.Infrastructure.AttachedProperties
{
    public static class WatermarkProperties
    {
        public static readonly DependencyProperty WatermarkProperty =
            DependencyProperty.RegisterAttached(
                "Watermark",
                typeof(object),
                typeof(WatermarkProperties),
                new FrameworkPropertyMetadata(null));

        public static object GetWatermark(DependencyObject d) => (object)d.GetValue(WatermarkProperty);
        public static void SetWatermark(DependencyObject d, object value) => d.SetValue(WatermarkProperty, value);

        // HasPassword (Read-only proxy)
        private static readonly DependencyPropertyKey HasPasswordPropertyKey =
            DependencyProperty.RegisterAttachedReadOnly(
                "HasPassword",
                typeof(bool),
                typeof(WatermarkProperties),
                new PropertyMetadata(false));

        public static readonly DependencyProperty HasPasswordProperty = HasPasswordPropertyKey.DependencyProperty;
        public static bool GetHasPassword(DependencyObject d) => (bool)d.GetValue(HasPasswordProperty);

        // MonitorPassword (attached to enable tracking)
        public static readonly DependencyProperty MonitorPasswordProperty =
            DependencyProperty.RegisterAttached(
                "MonitorPassword",
                typeof(bool),
                typeof(WatermarkProperties),
                new PropertyMetadata(false, OnMonitorPasswordChanged));

        public static bool GetMonitorPassword(DependencyObject d) => (bool)d.GetValue(MonitorPasswordProperty);
        public static void SetMonitorPassword(DependencyObject d, bool value) => d.SetValue(MonitorPasswordProperty, value);

        private static void OnMonitorPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox pb)
            {
                pb.PasswordChanged -= OnPasswordChanged;
                if ((bool)e.NewValue)
                {
                    pb.PasswordChanged += OnPasswordChanged;
                    UpdateHasPassword(pb);
                }
            }
        }

        private static void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb)
            {
                UpdateHasPassword(pb);
            }
        }

        private static void UpdateHasPassword(PasswordBox pb)
        {
            pb.SetValue(HasPasswordPropertyKey, pb.Password.Length > 0);
        }
    }
}
