using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OCC.Client.Data;
using OCC.Client.Infrastructure;
using OCC.Client.ViewModels.Messages; // Corrected
using OCC.Client.Services;
using OCC.Client.ViewModels; // For LoginViewModel
using OCC.Client.ViewModels.Shared; // Added for WorkHoursPopupViewModel
// using OCC.Shared.Infrastructure; // Removed as it seems incorrect or unused if NavigationRoutes is in Client.Infrastructure
using System;
using System.Threading; // Added for CancellationTokenSource
using System.Threading.Tasks; // Added for Task


using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Features.AuthHub.ViewModels;
using OCC.Client.Features.TimeAttendanceHub.ViewModels;
using OCC.Client.ViewModels.Notifications;
using OCC.Client.Messages;

namespace OCC.Client.ViewModels.Core
{
    /// <summary>
    /// ViewModel for the SideMenu navigation view.
    /// Manages the state of the sidebar (collapsed/expanded), navigation logic, user profile display, and quick actions.
    /// </summary>
    public partial class SideMenuViewModel : ViewModelBase, IRecipient<UpdateStatusMessage>
    {
        #region Private Members

        private readonly IAuthService _authService;
        private readonly IUpdateService _updateService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<SideMenuViewModel> _logger;
        private readonly IToastService _toastService;
        private readonly IReminderService _reminderService;

        #endregion

        #region Observables

        /// <summary>
        /// Gets or sets whether the sidebar is collapsed (minimized) or expanded.
        /// </summary>
        [ObservableProperty]
        private bool _isCollapsed;

        /// <summary>
        /// Gets or sets the currently active navigation section (e.g., "Home", "Projects").
        /// Used for highlighting the active button in the view.
        /// </summary>
        [ObservableProperty]
        private string _activeSection = NavigationRoutes.Home;

        /// <summary>
        /// Gets or sets the current user's email address for display in the profile section.
        /// </summary>
        [ObservableProperty]
        private string _userEmail = string.Empty;

        /// <summary>
        /// Gets or sets the current user's initials for the profile avatar.
        /// </summary>
        [ObservableProperty]
        private string _userInitials = string.Empty;

        /// <summary>
        /// Gets or sets a message displaying the last action taken, for user feedback.
        /// </summary>
        [ObservableProperty]
        private string _lastActionMessage = string.Empty;

        /// <summary>
        /// Controls the visibility (opacity) of the action message for fading effects.
        /// </summary>
        [ObservableProperty]
        private bool _isMessageVisible;

        private CancellationTokenSource? _messageCts;

        /// <summary>
        /// Gets or sets the application version string.
        /// </summary>
        [ObservableProperty]
        private string _appVersion = string.Empty;

        /// <summary>
        /// Gets or sets whether the "Quick Actions" popup menu is open.
        /// </summary>
        [ObservableProperty]
        private bool _isQuickActionsOpen;

        /// <summary>
        /// Gets or sets whether the "Settings" popup menu is open.
        /// </summary>
        [ObservableProperty]
        private bool _isSettingsOpen;

        /// <summary>
        /// Gets or sets whether the "Preferences" popup menu is open.
        /// </summary>
        [ObservableProperty]
        private bool _isPreferencesOpen;

        /// <summary>
        /// Gets whether the current user has permission to manage users.
        /// </summary>
        public bool CanManageUsers => _permissionService.CanAccess(NavigationRoutes.UserManagement);

        [ObservableProperty]
        private bool _isDarkMode;

        [ObservableProperty]
        private string _themeToggleText = "Enable Dark Mode";

        public bool CanViewStaff => _permissionService.CanAccess(NavigationRoutes.StaffManagement);
        public bool CanViewCustomers => _permissionService.CanAccess(NavigationRoutes.Customers);
        public bool CanAccessOrders => _permissionService.CanAccess(NavigationRoutes.Feature_OrderManagement);
        public bool CanAccessCompanySettings => _permissionService.CanAccess(NavigationRoutes.CompanySettings);
        public bool CanAccessCompanyProfile => _permissionService.CanAccess(NavigationRoutes.CompanyProfile);
        public bool CanViewProjects => _permissionService.CanAccess(NavigationRoutes.Projects);
        public bool CanViewDashboard => _permissionService.CanAccess(NavigationRoutes.Home);
        public bool CanViewTime => _permissionService.CanAccess(NavigationRoutes.Time);
        public bool CanAccessUserPreferences => _permissionService.CanAccess(NavigationRoutes.UserPreferences);
        public bool CanAccessAlerts => _permissionService.CanAccess(NavigationRoutes.Alerts);
        public bool CanAccessWages => _permissionService.CanAccess(NavigationRoutes.Feature_Wages);
        
        // Critical Security Fixes
        public bool CanAccessBugs => _permissionService.CanAccess(NavigationRoutes.Feature_BugReports);
        public bool CanAccessAuditLog => _permissionService.CanAccess(NavigationRoutes.AuditLog);
        public bool CanAccessHealthSafety => _permissionService.CanAccess(NavigationRoutes.HealthSafety);

        public bool IsDeveloper => _permissionService.IsDev;
        
        public bool CanCreateProject => _permissionService.CanAccess(NavigationRoutes.Feature_ProjectCreation);
        public bool CanViewCalendar => _permissionService.CanAccess(NavigationRoutes.Calendar);

        [ObservableProperty]
        private int _reminderCount;

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor for design-time support.
        /// </summary>
        public SideMenuViewModel()
        {
            _authService = null!;
            _updateService = null!;
            _serviceProvider = null!;
            _permissionService = null!;
            _logger = null!;
            _notificationViewModel = null!;
            _toastService = null!;
            _reminderService = null!;
        }

        /// <summary>
        /// Main constructor with dependency injection.
        /// </summary>
        private readonly NotificationViewModel _notificationViewModel;
        public NotificationViewModel NotificationVM => _notificationViewModel;

        public SideMenuViewModel(
            IAuthService authService, 
            IUpdateService updateService, 
            IServiceProvider serviceProvider, 
            IPermissionService permissionService, 
            ILogger<SideMenuViewModel> logger,
            NotificationViewModel notificationViewModel,
            IToastService toastService,
            IReminderService reminderService)
        {
            _authService = authService;
            _updateService = updateService;
            _serviceProvider = serviceProvider;
            _permissionService = permissionService;
            _logger = logger;
            _notificationViewModel = notificationViewModel;
            _toastService = toastService;
            _reminderService = reminderService;

            // Monitor Reminders
            _reminderService.UnreadCountChanged += (s, count) => ReminderCount = count;
            ReminderCount = _reminderService.UnreadCount;
            _reminderService.Start();

            // Register for messages
            WeakReferenceMessenger.Default.RegisterAll(this);

            // Initialize version info
            AppVersion = $"v{_updateService.CurrentVersion}";

            // Initialize user info
            if (_authService.CurrentUser != null)
            {
                UserEmail = _authService.CurrentUser.Email;
                UserInitials = GetInitials(_authService.CurrentUser.DisplayName);
            }

            // Monitor Notifications
            _notificationViewModel.Notifications.CollectionChanged += (s, e) => UpdateNotificationStatus();
            UpdateNotificationStatus(); // Initial check

            _ = StartAutoUpdateCheckAsync();
        }

        private void UpdateNotificationStatus()
        {
            var notes = _notificationViewModel.Notifications;
            bool hasUnread = false;
            bool hasPending = false;

            // Avoid iteration on UI thread if huge, but here it's small list
            foreach (var n in notes)
            {
                if (!n.IsRead) 
                {
                    hasUnread = true;
                    break; 
                }
                
                // Logic: If read, but pending action?
                // The user said: "If the messages are read but no action has been taken like approved or denied it must be orange"
                // My currently loaded notifications ARE pending actions (they are removed on approval).
                // So implies: any notification in list = pending action? 
                // Or I need to check properties. 
                // Currently `NotificationViewModel` ONLY holds pending/actionable items for Admins mainly (Approvals, Requests). 
                // So if there are ANY notifications, they effectively represent pending actions.
                // But the user distinguished between "Unread" (Red) and "Read but no action" (Orange).
                // So: Unread -> Red. Read -> Orange (if list not empty). Empty -> Gray.
                hasPending = true; 
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (hasUnread) NotificationIconColor = Avalonia.Media.Brushes.Red;
                else if (hasPending) NotificationIconColor = Avalonia.Media.Brushes.Orange;
                else NotificationIconColor = Avalonia.Media.Brushes.Black; // Default
            });
        }
        
        [ObservableProperty]
        private Avalonia.Media.IBrush _notificationIconColor = Avalonia.Media.Brushes.Gray;

        private async Task StartAutoUpdateCheckAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync())
            {
                await CheckForUpdates();
            }
        }

        public void Receive(UpdateStatusMessage message)
        {
            UpdateLastActionMessage(message.Value);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Toggles the visibility of the Quick Actions popup.
        /// Closes Settings popup if open.
        /// </summary>
        [RelayCommand]
        public void ToggleQuickActions()
        {
            IsQuickActionsOpen = !IsQuickActionsOpen;
            if (IsQuickActionsOpen) 
            {
                IsSettingsOpen = false;
                IsPreferencesOpen = false;
            }
        }

        /// <summary>
        /// Toggles the visibility of the Settings popup.
        /// Closes Quick Actions popup if open.
        /// </summary>
        [RelayCommand]
        public void ToggleSettings()
        {
            IsSettingsOpen = !IsSettingsOpen;
            if (IsSettingsOpen) 
            {
                IsQuickActionsOpen = false;
                IsPreferencesOpen = false;
            }
        }

        /// <summary>
        /// Toggles the visibility of the Preferences popup.
        /// Closes other popups if open.
        /// </summary>
        [RelayCommand]
        public void TogglePreferences()
        {
            IsPreferencesOpen = !IsPreferencesOpen;
            if (IsPreferencesOpen) 
            {
                IsQuickActionsOpen = false;
                IsSettingsOpen = false;
            }
        }

        /// <summary>
        /// Checks for application updates using the UpdateService.
        /// Displays a dialog if an update is available.
        /// </summary>
        [RelayCommand]
        public async Task CheckForUpdates()
        {
            UpdateLastActionMessage("Checking for updates...");
            var updateInfo = await _updateService.CheckForUpdatesAsync();
            
            if (updateInfo != null)
            {
                UpdateLastActionMessage("Update available!");
                
                // Show the update dialog on the UI thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    var dialog = new Views.Shared.UpdateDialogView();
                    dialog.DataContext = new UpdateDialogViewModel(_updateService, updateInfo, () => dialog.Close());
                    
                    if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null) 
                    {
                        dialog.ShowDialog(desktop.MainWindow);
                    }
                });
            }
            else
            {
                UpdateLastActionMessage("You are up to date.");
            }
        }

        /// <summary>
        /// Toggles the collapsed state of the sidebar.
        /// </summary>
        [RelayCommand]
        public void ToggleCollapse()
        {
            IsCollapsed = !IsCollapsed;
        }

        /// <summary>
        /// Navigates to a specific section of the application.
        /// Sends messages to switch tabs or open views based on the section.
        /// </summary>
        /// <param name="section"> The navigation route/section identifier. </param>
        [RelayCommand]
        private void Navigate(string section)
        {
            if (section == NavigationRoutes.Notifications)
            {
                UpdateLastActionMessage("Navigating to Notifications");
                WeakReferenceMessenger.Default.Send(new OpenNotificationsMessage());
                return;
            }

            if (section == NavigationRoutes.Reminders)
            {
                UpdateLastActionMessage("Navigating to Task Reminders");
                // Navigate to home and switch to list view where tasks can be seen
                ActiveSection = NavigationRoutes.Home;
                WeakReferenceMessenger.Default.Send(new SwitchTabMessage("List"));
                return;
            }

            if (section == "Suppliers")
            {
                UpdateLastActionMessage("Navigating to Suppliers");
                ActiveSection = "Orders"; // Highlight Orders in sidebar
                WeakReferenceMessenger.Default.Send(new SwitchTabMessage("Suppliers"));
                return;
            }

            UpdateLastActionMessage($"Navigating to {section}");
            ActiveSection = section;
            
            // Sync with TopBar tabs
            switch (section)
            {
                case NavigationRoutes.Home:
                    WeakReferenceMessenger.Default.Send(new SwitchTabMessage("My Summary"));
                    break;
                case NavigationRoutes.Time:
                    WeakReferenceMessenger.Default.Send(new SwitchTabMessage("Live"));
                    break;
                case NavigationRoutes.Projects:
                    WeakReferenceMessenger.Default.Send(new SwitchTabMessage("Projects"));
                    break;
                case NavigationRoutes.StaffManagement:
                    WeakReferenceMessenger.Default.Send(new SwitchTabMessage("Team"));
                    break;
                case NavigationRoutes.Calendar:
                    // Just navigate
                    break;
                case NavigationRoutes.Feature_Wages:
                    WeakReferenceMessenger.Default.Send(new SwitchTabMessage("WageRun"));
                    break;
            }
        }

        /// <summary>
        /// Logs out the current user and navigates to the Login view.
        /// </summary>
        [RelayCommand]
        private void Logout()
        {
            _authService.LogoutAsync();
            var loginVm = _serviceProvider.GetRequiredService<LoginViewModel>();
            WeakReferenceMessenger.Default.Send(new NavigationMessage(loginVm));
        }


        [RelayCommand]
        private void ToggleTheme() 
        {
            IsDarkMode = !IsDarkMode;
            App.ChangeTheme(IsDarkMode);
            ThemeToggleText = IsDarkMode ? "Enable Light Mode" : "Enable Dark Mode";
            
            // Close menu
            IsPreferencesOpen = false;
            UpdateLastActionMessage(IsDarkMode ? "Dark Mode Enabled" : "Light Mode Enabled");
        }

        [RelayCommand]
        public void GoToDevTest()
        {
             Navigate(NavigationRoutes.Developer);
             IsSettingsOpen = false;
        }



        [RelayCommand]
        private void Customers()
        {
            IsQuickActionsOpen = false;
            IsSettingsOpen = false;
            ActiveSection = NavigationRoutes.Customers;
            UpdateLastActionMessage("Navigating to Customers");
        }


        /// <summary>
        /// Navigates to the User Management section.
        /// </summary>
        [RelayCommand]
        private void UsersTeams() 
        {
             IsQuickActionsOpen = false;
             IsSettingsOpen = false;
             ActiveSection = NavigationRoutes.UserManagement;
            LastActionMessage = "Navigating to Manage Users";
             UpdateLastActionMessage("Navigating to Manage Users");
        }



        [RelayCommand]
        private void Alerts() { }

        [RelayCommand]
        private void AuditLog() 
        {
            IsQuickActionsOpen = false;
            IsSettingsOpen = false;
            ActiveSection = NavigationRoutes.AuditLog;
            UpdateLastActionMessage("Navigating to Audit Log");
        }



        [RelayCommand]
        private void CompanySettings()
        {
            IsQuickActionsOpen = false;
            IsSettingsOpen = false;
            ActiveSection = NavigationRoutes.CompanySettings;
            UpdateLastActionMessage("Navigating to System Settings");
        }

        [RelayCommand]
        private void CompanyProfile()
        {
            IsQuickActionsOpen = false;
            IsSettingsOpen = false;
            ActiveSection = NavigationRoutes.CompanyProfile;
            UpdateLastActionMessage("Navigating to Company Profile");
        }

        [RelayCommand]
        private void UserPreferences()
        {
            IsQuickActionsOpen = false;
            IsSettingsOpen = false;
            IsPreferencesOpen = false;
            ActiveSection = NavigationRoutes.UserPreferences;
            UpdateLastActionMessage("Navigating to User Preferences");
        }


        /// <summary>
        /// Navigates to the My Profile section.
        /// </summary>
        [RelayCommand]
        private void MyProfile() 
        { 
             IsQuickActionsOpen = false; 
             IsSettingsOpen = false;
             IsPreferencesOpen = false;
             ActiveSection = "MyProfile";
             // Instead of just setting ActiveSection, we should ensure the message is sent or Shell handles it.
             // ShellViewModel listens to ActiveSection property change.
             WeakReferenceMessenger.Default.Send(new OpenProfileMessage());
        }



        /// <summary>
        /// Triggers the creation of a new task.
        /// </summary>
        [RelayCommand]
        public void NewTask() 
        { 
            IsQuickActionsOpen = false;
            UpdateLastActionMessage("Action Triggered: New Task (t)");
            WeakReferenceMessenger.Default.Send(new CreateNewTaskMessage());
        }

        /// <summary>
        /// Triggers the creation of a new project.
        /// </summary>
        [RelayCommand]
        public void NewProject() 
        { 
            IsQuickActionsOpen = false;

            if (!CanCreateProject)
            {
                _toastService.ShowWarning("Permission Denied", "You do not have permission to create new projects.");
                return;
            }

            ActiveSection = NavigationRoutes.Home; // Ensure we are on Home View
            UpdateLastActionMessage("Action Triggered: New Project (p)");
            WeakReferenceMessenger.Default.Send(new CreateProjectMessage());
        }

        [RelayCommand]
        public void NewMeeting() 
        { 
            UpdateLastActionMessage("Action Triggered: New Meeting (m)");
        }

        [RelayCommand]
        public void InviteUser() 
        { 
            if (!CanManageUsers)
            {
                _toastService.ShowWarning("Permission Denied", "You do not have permission to invite new users.");
                return;
            }
            UpdateLastActionMessage("Action Triggered: Invite User (i)");
        }

        [RelayCommand]
        public void NewTeamMember() 
        { 
            UpdateLastActionMessage("Action Triggered: New Team Member");
        }

        [RelayCommand]
        public void SimulateBirthday()
        {
            IsQuickActionsOpen = false;
            WeakReferenceMessenger.Default.Send(new TestBirthdayMessage());
        }


        #endregion

        #region Helper Methods

        /// <summary>
        /// Generates initials from a display name (e.g., "John Doe" -> "JD").
        /// </summary>
        /// <param name="displayName">The full display name.</param>
        /// <returns>Up to 2 characters representing the initials.</returns>
        private string GetInitials(string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "U";
            var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }

        /// <summary>
        /// Updates the last action message and starts a timer to fade it out.
        /// </summary>
        /// <param name="message">The message to display.</param>
        private void UpdateLastActionMessage(string message)
        {
            // Cancel any existing timer
            _messageCts?.Cancel();
            _messageCts = new CancellationTokenSource();
            var token = _messageCts.Token;

            LastActionMessage = message;
            IsMessageVisible = true;

            // Start timer to hide message
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(5000, token);
                    if (!token.IsCancellationRequested)
                    {
                        // Update on UI thread
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => IsMessageVisible = false);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Ignore cancellation
                }
            });
        }

        #endregion
    }
}
