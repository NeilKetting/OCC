using System;
using System.Globalization;
using System.Windows.Data;
using OCC.Shared.DTOs;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class UserInitialsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatUserDto user)
            {
                string initials = "";
                if (!string.IsNullOrEmpty(user.FirstName)) initials += char.ToUpper(user.FirstName[0]);
                if (!string.IsNullOrEmpty(user.LastName)) initials += char.ToUpper(user.LastName[0]);
                return initials;
            }

            if (value is string name && !string.IsNullOrWhiteSpace(name))
            {
                // Split by common delimiters including email symbols and brackets
                var parts = name.Split(new[] { ' ', ',', '-', '.', '@', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Filter parts to ensure they start with a letter or digit
                var filteredParts = parts.Where(p => char.IsLetterOrDigit(p[0])).ToList();

                if (filteredParts.Count >= 2)
                {
                    return $"{char.ToUpper(filteredParts[0][0])}{char.ToUpper(filteredParts[filteredParts.Count - 1][0])}";
                }
                if (filteredParts.Count == 1)
                {
                    return char.ToUpper(filteredParts[0][0]).ToString();
                }
            }

            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
