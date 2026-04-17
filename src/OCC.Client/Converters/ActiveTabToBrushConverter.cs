using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a tab name (string) and an active tab name (parameter) into a Brush color.
    /// Used to highlight the currently selected tab in navigation menus.
    /// 
    /// Used in:
    /// - ProjectTopBarView.axaml
    /// </summary>
    public class ActiveTabToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var activeTab = value as string;
            var buttonTab = parameter as string;

            if (string.IsNullOrEmpty(activeTab) || string.IsNullOrEmpty(buttonTab))
                return Brushes.Transparent;

            return activeTab == buttonTab 
                ? new SolidColorBrush(Color.Parse("#2563EB")) // Active: Blue
                : Brushes.Transparent; // Inactive
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
