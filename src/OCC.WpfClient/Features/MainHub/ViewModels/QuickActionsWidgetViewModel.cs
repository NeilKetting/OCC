using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Infrastructure;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.MainHub.ViewModels
{
    public partial class QuickActionsWidgetViewModel : WidgetViewModelBase
    {
        private readonly IPermissionService _permissionService;
        private readonly LocalSettingsService _localSettingsService;

        [ObservableProperty]
        private bool _isShortcutPickerOpen;

        public ObservableCollection<QuickShortcutOption> ActiveQuickActions { get; } = new();
        public ObservableCollection<QuickShortcutOption> ShortcutOptions { get; } = new();

        public QuickActionsWidgetViewModel(IPermissionService permissionService, LocalSettingsService localSettingsService)
        {
            _permissionService = permissionService;
            _localSettingsService = localSettingsService;
            WidgetId = "QuickActions";
            Title = "Quick Actions";
        }

        [RelayCommand]
        private void NavigateToRoute(string route)
        {
            if (string.IsNullOrEmpty(route)) return;
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(route));
        }

        [RelayCommand]
        private void OpenShortcutPicker()
        {
            foreach (var opt in ShortcutOptions)
            {
                opt.IsSelected = ActiveQuickActions.Any(a => a.Route == opt.Route);
            }
            IsShortcutPickerOpen = true;
        }

        [RelayCommand]
        private void SaveShortcuts()
        {
            var selectedRoutes = ShortcutOptions.Where(o => o.IsSelected).Select(o => o.Route).ToList();
            _localSettingsService.Settings.QuickActions = selectedRoutes;
            _localSettingsService.Save();
            
            RefreshActiveQuickActions(selectedRoutes);
            IsShortcutPickerOpen = false;
        }

        [RelayCommand]
        private void CancelShortcuts()
        {
            IsShortcutPickerOpen = false;
        }

        public override Task RefreshDataAsync()
        {
            InitializeQuickActions();
            return Task.CompletedTask;
        }

        private void InitializeQuickActions()
        {
            var allShortcuts = new System.Collections.Generic.List<QuickShortcutOption>
            {
                new QuickShortcutOption { Label = "Log Attendance", Route = NavigationRoutes.AttendanceLive, IconCode = "\uE916", PermissionKey = NavigationRoutes.AttendanceLive },
                new QuickShortcutOption { Label = "View Snags", Route = NavigationRoutes.SnagList, IconCode = "\uE72A", PermissionKey = NavigationRoutes.SnagList },
                new QuickShortcutOption { Label = "System Settings", Route = NavigationRoutes.CompanySettings, IconCode = "\uE713", PermissionKey = NavigationRoutes.CompanySettings },
                new QuickShortcutOption { Label = "Chat Hub", Route = NavigationRoutes.Chat, IconCode = "\uE8BD", PermissionKey = NavigationRoutes.Chat },
                new QuickShortcutOption { Label = "Calendar", Route = NavigationRoutes.Calendar, IconCode = "\uE787", PermissionKey = NavigationRoutes.Calendar },
                new QuickShortcutOption { Label = "Projects Portfolio", Route = NavigationRoutes.Projects, IconCode = "\uE82D", PermissionKey = NavigationRoutes.Projects },
                new QuickShortcutOption { Label = "Project Dashboard", Route = NavigationRoutes.ProjectDashboard, IconCode = "\uE9D9", PermissionKey = NavigationRoutes.ProjectDashboard },
                new QuickShortcutOption { Label = "Suppliers", Route = NavigationRoutes.Suppliers, IconCode = "\uE716", PermissionKey = NavigationRoutes.Suppliers },
                new QuickShortcutOption { Label = "Inventory", Route = NavigationRoutes.Inventory, IconCode = "\uE950", PermissionKey = NavigationRoutes.Inventory },
                new QuickShortcutOption { Label = "Purchase Orders", Route = NavigationRoutes.PurchaseOrder, IconCode = "\uE8A1", PermissionKey = NavigationRoutes.PurchaseOrder },
                new QuickShortcutOption { Label = "Picking Orders", Route = NavigationRoutes.Picking, IconCode = "\uE73E", PermissionKey = NavigationRoutes.Picking },
                new QuickShortcutOption { Label = "Subcontractors", Route = NavigationRoutes.SubContractors, IconCode = "\uE77B", PermissionKey = NavigationRoutes.SubContractors },
                new QuickShortcutOption { Label = "HSEQ", Route = NavigationRoutes.HealthSafety, IconCode = "\uEA18", PermissionKey = NavigationRoutes.HealthSafety },
                new QuickShortcutOption { Label = "Audit Log", Route = NavigationRoutes.AuditLog, IconCode = "\uE81C", PermissionKey = NavigationRoutes.AuditLog },
                new QuickShortcutOption { Label = "User Management", Route = NavigationRoutes.UserManagement, IconCode = "\uE77B", PermissionKey = NavigationRoutes.UserManagement }
            };

            var allowedOptions = allShortcuts.Where(o => string.IsNullOrEmpty(o.PermissionKey) || _permissionService.CanAccess(o.PermissionKey)).ToList();

            ShortcutOptions.Clear();
            foreach (var opt in allowedOptions)
            {
                ShortcutOptions.Add(opt);
            }

            var savedRoutes = _localSettingsService.Settings.QuickActions;
            if (savedRoutes == null)
            {
                savedRoutes = new System.Collections.Generic.List<string> { NavigationRoutes.AttendanceLive, NavigationRoutes.SnagList, NavigationRoutes.CompanySettings };
                _localSettingsService.Settings.QuickActions = savedRoutes;
                _localSettingsService.Save();
            }

            RefreshActiveQuickActions(savedRoutes);
        }

        private void RefreshActiveQuickActions(System.Collections.Generic.List<string> savedRoutes)
        {
            ActiveQuickActions.Clear();
            foreach (var route in savedRoutes)
            {
                var match = ShortcutOptions.FirstOrDefault(o => o.Route == route);
                if (match != null)
                {
                    ActiveQuickActions.Add(match);
                }
            }
        }
    }

    public partial class QuickShortcutOption : ObservableObject
    {
        public string Label { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string IconCode { get; set; } = string.Empty;
        public string PermissionKey { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }
}
