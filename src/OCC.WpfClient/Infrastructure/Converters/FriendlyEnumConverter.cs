using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;
using OCC.Shared.Models;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class FriendlyEnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            if (value is AttendanceStatus status)
            {
                if (status == AttendanceStatus.UnpaidHalfDay)
                {
                    return "Unpaid Half Day";
                }
                if (status == AttendanceStatus.UnpaidSick || status == AttendanceStatus.UnpaidLeave)
                {
                    return "Unpaid";
                }
            }

            if (value is Enum enumVal)
            {
                var fi = enumVal.GetType().GetField(enumVal.ToString());
                if (fi != null)
                {
                    var attributes = (System.ComponentModel.DescriptionAttribute[])fi.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
                    if (attributes.Length > 0)
                    {
                        return attributes[0].Description;
                    }
                }
            }

            string? enumString = value?.ToString();
            if (string.IsNullOrEmpty(enumString)) return string.Empty;

            if (enumString.Equals("UnpaidSick", StringComparison.OrdinalIgnoreCase) || 
                enumString.Equals("UnpaidLeave", StringComparison.OrdinalIgnoreCase) ||
                enumString.Equals("Unpaid-Sick", StringComparison.OrdinalIgnoreCase))
            {
                return "Unpaid";
            }

            // Split by capital letters: "SiteManager" -> "Site Manager"
            return Regex.Replace(enumString, "([a-z])([A-Z])", "$1 $2");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
