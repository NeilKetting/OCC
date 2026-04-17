using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    /// <summary>
    /// Converts a boolean status (Approved/Pending) to a color Brush.
    /// True (Approved) -> Green, False (Pending) -> Orange.
    /// 
    /// Used in:
    /// - ManageUsersView.axaml
    /// </summary>
    public class BoolToStatusColorConverter : IValueConverter
    {
        public static readonly BoolToStatusColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isApproved && isApproved)
            {
                return Brushes.Green; // Approved
            }
            return Brushes.Orange; // Pending
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean status (Approved/Pending) to text representation.
    /// True -> "APPROVED", False -> "PENDING".
    /// 
    /// Used in:
    /// - ManageUsersView.axaml
    /// </summary>
    public class BoolToStatusTextConverter : IValueConverter
    {
        public static readonly BoolToStatusTextConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isApproved && isApproved)
            {
                return "APPROVED";
            }
            return "PENDING";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
