using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OCC.Client.Converters
{
    /// <summary>
    /// A collection of static converters used for the Sidebar logic.
    /// - CollapseWidth: Calculates sidebar width (70 vs 250).
    /// - BoolToRotation: Rotates chevron based on expansion.
    /// - CollapseIcon: Switches chevron icon resource.
    /// - ActiveColor: Highlights the active navigation item.
    /// 
    /// Used in:
    /// - SideMenuView.axaml
    /// </summary>
    public class SidebarConverters
    {
        public static readonly IValueConverter CollapseWidth = 
            new FuncValueConverter<bool, double>(isCollapsed => isCollapsed ? 70.0 : 250.0);

        // Rotates -90 if NOT expanded (collapsed list), 0 if expanded
        public static readonly IValueConverter BoolToRotation = 
            new FuncValueConverter<bool, double>(isExpanded => isExpanded ? 0 : -90);

        public static readonly IValueConverter CollapseIcon = 
            new FuncValueConverter<bool, object?>(isCollapsed => 
            {
                var key = isCollapsed ? "IconChevronRight" : "IconChevronLeft";
                if (Application.Current != null && Application.Current.TryGetResource(key, null, out var resource))
                {
                    return resource;
                }
                return null;
            });

        public static readonly IValueConverter ActiveColor = new ActiveColorConverter();
    }

    /// <summary>
    /// Highlights the active sidebar section by comparing the bound section string with a parameter.
    /// Returns Blue if match, Gray if not.
    /// 
    /// Used in:
    /// - SideMenuView.axaml
    /// </summary>
    public class ActiveColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var activeSection = value as string;
            var targetSection = parameter as string;
            return activeSection == targetSection 
                ? new SolidColorBrush(Color.Parse("#2563eb")) 
                : new SolidColorBrush(Color.Parse("#64748b"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Highlights the active sidebar section by comparing the bound section string with a parameter.
}
