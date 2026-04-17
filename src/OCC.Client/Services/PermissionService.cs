using OCC.Client.Infrastructure;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;
using System;
using System.Linq;

namespace OCC.Client.Services
{
    /// <summary>
    /// Service responsible for determining user access and permissions based on their role.
    /// </summary>
    public class PermissionService : IPermissionService
    {
        private readonly IAuthService _authService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionService"/> class.
        /// </summary>
        /// <param name="authService">The authentication service to retrieve the current user.</param>
        public PermissionService(IAuthService authService)
        {
            _authService = authService;
        }

        public bool IsDev
        {
            get
            {
                var user = _authService.CurrentUser;
                if (user == null) return false;
                
                var email = user.Email?.ToLowerInvariant();
                return email == "neil@mdk.co.za" || email == "neil@origize63.co.za";
            }
        }

        /// <summary>
        /// Determines if the current user has access to a specific route or feature.
        /// </summary>
        /// <param name="route">The navigation route or feature key to check access for.</param>
        /// <returns>True if the user is authorized; otherwise, false.</returns>
        public bool CanAccess(string route)
        {
            var user = _authService.CurrentUser;
            if (user == null) return false;

            // 1. System Overrides (Bypass)
            if (IsDev || user.UserRole == UserRole.Admin || user.UserRole == UserRole.SiteManager) 
                return true;

            // 2. Global Deny (Sensitive/Restricted areas for all other roles)
            var restricted = new[] 
            { 
                NavigationRoutes.AuditLog, 
                NavigationRoutes.CompanySettings, 
                NavigationRoutes.CompanyProfile,
                NavigationRoutes.UserManagement, 
                NavigationRoutes.Feature_UserManagement, 
                NavigationRoutes.Feature_UserRegistration, 
                NavigationRoutes.Feature_WageViewing,
                NavigationRoutes.Developer 
            };
            if (restricted.Contains(route, StringComparer.OrdinalIgnoreCase)) return false;

            // 3. Global Allow (Safe areas for all authenticated staff roles)
            var basic = new[] 
            { 
                NavigationRoutes.Home, 
                NavigationRoutes.Calendar, 
                NavigationRoutes.Notifications, 
                NavigationRoutes.UserPreferences, 
                NavigationRoutes.Feature_BugReports, 
                NavigationRoutes.Alerts, 
                NavigationRoutes.Reminders,
                NavigationRoutes.AccessDenied,
                "MyProfile",
                "Help",
                "SendLogs"
            };
            if (basic.Contains(route, StringComparer.OrdinalIgnoreCase)) return true;

            // 4. Role-Based Whitelisting (Office)
            if (user.UserRole == UserRole.Office)
            {
                // A. Handle Orders Sub-Navigation (Group logic)
                if (route == "OrderList" || route == "Suppliers" || route == "CreateOrder")
                    return HasPermission(user, NavigationRoutes.Feature_OrderManagement);

                if (route == "Inventory" || route == "ItemList" || route == "RestockReview")
                    return HasPermission(user, NavigationRoutes.Feature_OrderManagement) || 
                           HasPermission(user, NavigationRoutes.Feature_OrderInventoryOnly);

                // B. Handle Toggleable Modules
                var toggleable = new[] 
                { 
                    NavigationRoutes.Time, 
                    NavigationRoutes.StaffManagement, 
                    NavigationRoutes.Projects, 
                    NavigationRoutes.Customers, 
                    NavigationRoutes.HealthSafety, 
                    NavigationRoutes.Feature_OrderManagement, 
                    NavigationRoutes.Feature_OrderInventoryOnly 
                };

                if (toggleable.Contains(route, StringComparer.OrdinalIgnoreCase))
                {
                    // Special case: "Orders" hub links are allowed if they have either Full or InventoryOnly permission
                    if (route == NavigationRoutes.Feature_OrderManagement)
                    {
                        return HasPermission(user, NavigationRoutes.Feature_OrderManagement) || 
                               HasPermission(user, NavigationRoutes.Feature_OrderInventoryOnly);
                    }

                    return HasPermission(user, route);
                }

                // C. Default Deny for Office (Securely catch-all for any unhandled routes)
                return false;
            }

            // 5. Contractor/Guest Access (Strict Minimal fallbacks)
            if (user.UserRole == UserRole.ExternalContractor || user.UserRole == UserRole.Guest)
            {
                return route switch
                {
                    NavigationRoutes.Projects => true, 
                    NavigationRoutes.Time => true, 
                    _ => false
                };
            }

            return false;
        }

        private bool HasPermission(User user, string route)
        {
            if (string.IsNullOrEmpty(user.Permissions)) return false;
            var allowed = user.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return allowed.Contains(route, StringComparer.OrdinalIgnoreCase);
        }
    }
}
