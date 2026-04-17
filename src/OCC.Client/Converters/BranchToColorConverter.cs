using Avalonia.Data.Converters;
using Avalonia.Media;
using OCC.Shared.Models;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class BranchToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Branch branch)
            {
                return branch switch
                {
                    Branch.JHB => Color.Parse("#00b5ad"), // PrimaryTeal
                    Branch.CPT => Color.Parse("#F97637"), // AccentOrange
                    _ => Color.Parse("#64748b") // TextSecondaryGray
                };
            }
            return Color.Parse("#64748b");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
