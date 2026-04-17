using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Linq;

using OCC.Client.Infrastructure;

namespace OCC.Client.Converters
{
    public class NotificationActionVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string targetAction && parameter is string buttonType)
            {
                // Logic based on TargetAction and Button Type
                switch (buttonType)
                {
                    case "Approve":
                    case "Deny":
                        return targetAction == NavigationRoutes.Feature_UserRegistration || 
                               targetAction == NavigationRoutes.Feature_LeaveApproval || 
                               targetAction == NavigationRoutes.Feature_OvertimeRequest;
                    
                    case "View":
                        return targetAction == NavigationRoutes.Feature_UserRegistration || 
                               targetAction == NavigationRoutes.Feature_LeaveApproval || 
                               targetAction == NavigationRoutes.Feature_OvertimeRequest || 
                               targetAction == NavigationRoutes.Feature_BugReports;
                               
                    default:
                        return true;
                }
            }
            
            // If TargetAction is null/empty, usually return false for these buttons
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
