using System;
using OCC.Shared.Models;
using OCC.Client.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OCC.Client.Infrastructure;

using OCC.Client.Features.TimeAttendanceHub.ViewModels;

namespace OCC.Client.Features.EmployeeHub.ViewModels
{
    public partial class EmployeePermissionsViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _userName = string.Empty;

        public partial class PermissionItem : ObservableObject
        {
            public string Key { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            
            [ObservableProperty]
            private bool _isSelected;
        }

        [ObservableProperty]
        private List<PermissionItem> _permissions = new();

        public System.Action<string?>? OnSaved;

        public EmployeePermissionsViewModel(string userName, string? currentPermissions, UserRole role)
        {
            UserName = userName;
            
            // Define all possible permissions
            // Filter: Strictly show only the 5 core toggles for any staff members managed here.
            // Admins and SiteManagers are granted full access via direct role checks elsewhere and don't need toggles.
            var filtered = new[] 
            { 
                NavigationRoutes.Time, 
                NavigationRoutes.StaffManagement, 
                NavigationRoutes.Projects, 
                NavigationRoutes.Feature_OrderManagement, 
                NavigationRoutes.HealthSafety 
            };

            // Use the actual navigation route keys for better consistency
            var finalPermissionsDisplay = new List<(string Key, string DisplayName)>
            {
                (NavigationRoutes.Time, "Time & Attendance"),
                (NavigationRoutes.StaffManagement, "Employee Management"),
                (NavigationRoutes.Customers, "Customer Management"),
                (NavigationRoutes.Projects, "Projects Portfolio"),
                (NavigationRoutes.Feature_OrderManagement, "Orders & Restocking"),
                (NavigationRoutes.Feature_OrderInventoryOnly, "Order Inventory Only"),
                (NavigationRoutes.HealthSafety, "Health & Safety")
            };

            var current = (currentPermissions ?? string.Empty)
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

            bool isNew = string.IsNullOrEmpty(currentPermissions);

            Permissions = finalPermissionsDisplay.Select(p => new PermissionItem
            {
                Key = p.Key,
                DisplayName = p.DisplayName,
                IsSelected = isNew || current.Contains(p.Key, System.StringComparer.OrdinalIgnoreCase)
            }).ToList();
        }

        [RelayCommand]
        private void Save()
        {
            var selected = Permissions.Where(p => p.IsSelected).Select(p => p.Key);
            var permissionsString = string.Join(",", selected);
            OnSaved?.Invoke(permissionsString);
        }
    }
}
