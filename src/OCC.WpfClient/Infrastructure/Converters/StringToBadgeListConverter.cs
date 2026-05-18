using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using OCC.WpfClient.Features.ProjectHub.ViewModels;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class StringToBadgeListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrWhiteSpace(str))
            {
                var names = str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(s => s.Trim())
                               .Where(s => !string.IsNullOrEmpty(s))
                               .ToList();

                var badges = new List<BadgeData>();
                foreach (var name in names)
                {
                    string bgColor = "#2E9DFF"; // Default
                    string fgColor = null;

                    if (name.Equals("OCC", StringComparison.OrdinalIgnoreCase) || 
                        name.StartsWith("Orange Circle", StringComparison.OrdinalIgnoreCase))
                    {
                        bgColor = "#FF9800"; // Orange for OCC internal records
                        fgColor = "White";    // White foreground
                    }
                    else if (ProjectTaskListViewModel.SubContractorColorMap.TryGetValue(name, out var color))
                    {
                        bgColor = color;
                    }

                    if (fgColor == null)
                    {
                        fgColor = GetContrastColor(bgColor);
                    }

                    badges.Add(new BadgeData
                    {
                        Name = name,
                        Initials = GetInitials(name),
                        BackgroundColor = bgColor,
                        ForegroundColor = fgColor
                    });
                }
                return badges;
            }
            return new List<BadgeData>();
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "--";
            
            var parts = name.Split(new[] { ' ', '-', '.' }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(p => char.IsLetterOrDigit(p[0]))
                            .ToList();

            if (parts.Count >= 2)
            {
                return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[parts.Count - 1][0])}";
            }
            if (parts.Count == 1)
            {
                return parts[0].Length >= 2 
                    ? parts[0].Substring(0, 2).ToUpper() 
                    : parts[0][0].ToString().ToUpper();
            }
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private string GetContrastColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor) || hexColor.Length < 7) return "White";
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexColor);
                // Standard formula for relative luminance
                double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
                return luminance > 0.5 ? "Black" : "White";
            }
            catch { return "White"; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
