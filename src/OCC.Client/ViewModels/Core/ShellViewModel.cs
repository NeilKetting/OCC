using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OCC.Client.Features.HomeHub.ViewModels;
using OCC.Client.Features.HomeHub.ViewModels.Dashboard;
using OCC.Client.Features.HomeHub.ViewModels.Shared;
using OCC.Client.Features.HseqHub.ViewModels;
using OCC.Client.Features.OrdersHub.ViewModels;
using OCC.Client.Features.ProjectsHub.ViewModels;
using OCC.Client.Features.CalendarHub.ViewModels;
using OCC.Client.Features.TaskHub.ViewModels;
using OCC.Client.Features.BugHub.ViewModels; // Added
using OCC.Client.Features.EmployeeHub.ViewModels;
using OCC.Client.Features.TimeAttendanceHub.ViewModels;
using OCC.Client.Features.CustomerHub.ViewModels;
using OCC.Client.Features.SettingsHub.ViewModels;
using OCC.Client.Features.AuthHub.ViewModels;
using OCC.Client.ViewModels.Shared; // For ProfileViewModel
using OCC.Client.ViewModels.Messages;
using OCC.Client.Features.WagesHub.ViewModels;
using OCC.Client.Services;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using System.Linq;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Infrastructure;
using OCC.Client.Messages; // NEW
using OCC.Client.ViewModels.Notifications;
using OCC.Client.ViewModels.Core;
using OCC.Client.Features.AuthHub.Views; // Added
// using OCC.Client.Views.Login; // Removed Duplicate
using OCC.Shared.Models; // Added for Employee

namespace OCC.Client.ViewModels.Core
{
    public partial class WorkspaceViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title;
        
        [ObservableProperty]
        private string _id;

        [ObservableProperty]
        private object _iconData;

        [ObservableProperty]
        private ViewModelBase _viewModel;

        [ObservableProperty]
        private bool _isActive;

        public WorkspaceViewModel(string title, string id, object iconData, ViewModelBase viewModel)
        {
            _title = title;
            _id = id;
            _iconData = iconData;
            _viewModel = viewModel;
        }
    }

    public partial class ShellViewModel : ViewModelBase, 
        IRecipient<OpenNotificationsMessage>,
        IRecipient<OpenManageUsersMessage>,
        IRecipient<TestBirthdayMessage>,
        IRecipient<OpenProfileMessage>,
        IRecipient<ToastNotificationMessage>,
        IRecipient<OpenBugReportMessage>,
        IRecipient<ProjectSettingsRequestedMessage>,
        IRecipient<CreateNewTaskMessage>
    {

        #region Private Members

        private readonly IServiceProvider _serviceProvider;
        private readonly IPermissionService _permissionService;
        private readonly SignalRNotificationService _signalRService;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly IRepository<Project> _projectRepo;
        private string _previousSection = Infrastructure.NavigationRoutes.Home;
        private string _currentSection = Infrastructure.NavigationRoutes.Home;
        private bool _isSyncingSideMenu;

        #endregion

        #region Observables

        [ObservableProperty]
        private SideMenuViewModel _sideMenuViewModel;

        // Replaces _currentPage
        [ObservableProperty]
        private WorkspaceViewModel _currentWorkspace;

        partial void OnCurrentWorkspaceChanged(WorkspaceViewModel? oldValue, WorkspaceViewModel newValue)
        {
            if (oldValue != null) oldValue.IsActive = false;
            if (newValue != null) 
            {
                newValue.IsActive = true;
                
                // Sync SideMenu
                if (_sideMenuViewModel != null)
                {
                    _isSyncingSideMenu = true;
                    try
                    {
                        // Special case for 'CreateOrder' which has a unique ID but maps to Orders section
                        if (newValue.Id.StartsWith("CreateOrder"))
                        {
                            _sideMenuViewModel.ActiveSection = Infrastructure.NavigationRoutes.Feature_OrderManagement;
                        }
                        else if (newValue.Id == "Beta" || newValue.Id == "ReleaseNotes")
                        {
                            // Maintain current or set to specific if needed
                        }
                        else
                        {
                            _sideMenuViewModel.ActiveSection = newValue.Id;
                        }
                    }
                    finally
                    {
                        _isSyncingSideMenu = false;
                    }
                }
            }
        }

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<WorkspaceViewModel> _workspaces = new();

        [ObservableProperty]
        private NotificationViewModel _notificationVM;

        [ObservableProperty]
        private Shared.ProfileViewModel? _currentProfile;

        [ObservableProperty]
        private bool _isProfileOpen;

        [ObservableProperty]
        private bool _isNotificationOpen;

        [ObservableProperty]
        private TaskDetailViewModel? _currentTaskDetail;

        [ObservableProperty]
        private bool _isTaskDetailVisible;

        [ObservableProperty]
        private bool _isAuthenticated;

        [ObservableProperty]
        private int _onlineCount;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<UserDisplayModel> _connectedUsers = new();

        [ObservableProperty]
        private string _userActivityStatus = "Active";

        [ObservableProperty]
        private bool _isDbConnected;

        [ObservableProperty]
        private string _dbStatusText = "Checking...";

        [ObservableProperty]
        private bool _isLogUploading;

        [ObservableProperty]
        private string _logUploadStatus = string.Empty;

        public System.Collections.ObjectModel.ObservableCollection<Models.ToastMessage> Toasts { get; } = new();

        #endregion

        #region Constructors

        public ShellViewModel()
        {
            // Parameterless constructor for design-time support
            _serviceProvider = null!;
            _permissionService = null!;
            _sideMenuViewModel = null!;
            _currentWorkspace = null!;
            _signalRService = null!;
            _notificationVM = null!;
            _authService = null!;
            _dialogService = null!;
            _toastService = null!;
            _projectRepo = null!;
        }

        public ShellViewModel(
            IServiceProvider serviceProvider, 
            SideMenuViewModel sideMenuViewModel, 
            IUpdateService updateService, 
            IPermissionService permissionService,
            SignalRNotificationService signalRService,
            UserActivityService userActivityService,
            IDialogService dialogService,
            IToastService toastService,
            IAuthService authService)
        {
            _serviceProvider = serviceProvider;
            _permissionService = permissionService;
            _signalRService = signalRService;
            _authService = authService;
            _dialogService = dialogService;
            _toastService = toastService;

            // Initialize Project Repo for Profile
            _projectRepo = _serviceProvider.GetRequiredService<IRepository<Project>>();
            
            _signalRService.OnUserListReceived += OnUserUiUpdate;
            _signalRService.OnBroadcastReceived += (sender, message) => 
            {
                _toastService.ShowInfo($"Broadcast from {sender}", message);
            };

            // Subscribe to Navigation Requests
            WeakReferenceMessenger.Default.Register<NavigationRequestMessage>(this, (r, m) =>
            {
                HandleNavigationRequest(m.Value, m.Payload);
            });
            
            // Subscribe to Log Upload Status
            WeakReferenceMessenger.Default.Register<LogUploadStatusMessage>(this, (r, m) =>
            {
                IsLogUploading = m.IsUploading;
                LogUploadStatus = m.Value;

                if (m.IsSuccess) _toastService.ShowSuccess("Logs Uploaded", "Application logs have been sent successfully.");
                if (m.IsError) _toastService.ShowError("Upload Failed", m.Value);
            });
            
            // User Activity
            UserActivityStatus = userActivityService.StatusText;
            userActivityService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(UserActivityService.StatusText))
                {
                    UserActivityStatus = userActivityService.StatusText;
                }
            };
            userActivityService.SessionWarning += OnSessionWarning;
            userActivityService.SessionExpired += OnSessionExpired;

            _sideMenuViewModel = sideMenuViewModel;
            _sideMenuViewModel.PropertyChanged += SideMenu_PropertyChanged;

            _currentWorkspace = null!; // Silence warning as NavigateTo sets it
            
            // Initialize persistent Notification ViewModel
            _notificationVM = _serviceProvider.GetRequiredService<NotificationViewModel>();

            // Default to Home (Dashboard) unless Beta Notice is pending
            var currentVersion = updateService.CurrentVersion;
            
            if (!ReleaseNotes.BetaNoticeViewModel.IsNoticeAccepted(currentVersion))
            {
                var betaVM = new ReleaseNotes.BetaNoticeViewModel(currentVersion);
                betaVM.Accepted += () => 
                {
                    NavigateTo(Infrastructure.NavigationRoutes.Home);
                };
                betaVM.OpenReleaseNotesRequested += () =>
                {
                    var releaseNotesVM = new ViewModels.Help.ReleaseNotesViewModel();
                    releaseNotesVM.CloseRequested += (s, e) => 
                    {
                        // Return to Beta Notice
                        // For Beta Notice, we might just swap the VM in the current temporary workspace
                        // But since we are moving to workspaces, let's just make Beta Notice a workspace for now
                         AddOrActivateWorkspace("Beta Notice", "Beta", null, betaVM);
                    };
                    AddOrActivateWorkspace("Release Notes", "ReleaseNotes", null, releaseNotesVM);
                };
                 AddOrActivateWorkspace("Welcome", "Beta", null, betaVM);
            }
            else
            {
                NavigateTo(Infrastructure.NavigationRoutes.Home);
            }

            WeakReferenceMessenger.Default.RegisterAll(this); // Register for messages

            // Start/Restart SignalR Connection Globally to ensure Auth Token is used
            _ = _signalRService.RestartAsync();

            // Auto-Update removed from here - moved to App Startup
            
            // Check Birthdays
            CheckBirthdaysAsync(serviceProvider.GetRequiredService<IRepository<Employee>>());

            // Subscribe to Connection Settings
            ConnectionSettings.Instance.PropertyChanged += async (s, e) => 
            {
               if (e.PropertyName == nameof(ConnectionSettings.SelectedEnvironment) || e.PropertyName == nameof(ConnectionSettings.ApiBaseUrl))
               {
                   await CheckDbConnection();
                   await _signalRService.RestartAsync();
               }
            };

            // Start DB Polling
            StartDbPolling();
        }

        private async void StartDbPolling()
        {
            // Initial check
            await CheckDbConnection();

            // Poll every 30 seconds
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync())
            {
                await CheckDbConnection();
            }
        }

        private async Task CheckDbConnection()
        {
            try
            {
                // Call the Health Check endpoint to get the actual DB Name
                // We use a raw HttpClient here to avoid circular dependencies or complex service modifications for now
                using var client = new System.Net.Http.HttpClient();
                client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl);
                
                var response = await client.GetAsync("api/health/db-check");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    // Simple JSON parsing to avoid pulling in models just for this
                    // Expected: { ... "databaseName": "OCC_DB_DEV", ... }
                    
                    string dbName = "Unknown";
                    try 
                    {
                        var json = System.Text.Json.JsonDocument.Parse(content);
                        if (json.RootElement.TryGetProperty("databaseName", out var dbProp))
                        {
                            dbName = dbProp.GetString() ?? "Unknown";
                        }
                    }
                    catch
                    {
                         dbName = "Parse Error";
                    }

                    IsDbConnected = true;
                    // Append connection type for clarity (Local only, Live is default)
                    var type = ConnectionSettings.Instance.SelectedEnvironment == ConnectionSettings.AppEnvironment.Local ? "(Local)" : "";
                    DbStatusText = $"Online: {dbName} {type}";
                }
                else
                {
                    throw new Exception($"Status Code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellViewModel] DB Connection Check Failed: {ex.Message}");
                IsDbConnected = false;
                DbStatusText = $"Offline: {ConnectionSettings.Instance.SelectedEnvironment}";
            }
        }

        private async void CheckBirthdaysAsync(IRepository<Employee> employeeRepository)
        {
             // Run on background initially but Dialog must be on UI
             await Task.Delay(2000); // Wait for things to settle

             try
             {
                 // Get Preferences Service
                 var prefsService = _serviceProvider.GetRequiredService<OCC.Client.Services.Infrastructure.UserPreferencesService>();
                 if (prefsService.Preferences.LastBirthdayWishYear == DateTime.Now.Year)
                 {
                     return; // Already acknowledged this year
                 }

                 var today = DateTime.Today;
                 var employees = await employeeRepository.GetAllAsync();
                 var birthdayPeople = employees.Where(e => e.Status == EmployeeStatus.Active && 
                                                      e.DoB.Date.Month == today.Month && 
                                                      e.DoB.Date.Day == today.Day).ToList();

                 if (!birthdayPeople.Any()) return;

                 var currentUser = _authService.CurrentUser;
                 
                 foreach (var person in birthdayPeople)
                 {
                     // Personal Wish
                     // Check if this person IS the current user
                     // We link by LinkedUserId
                     if (currentUser != null && person.LinkedUserId == currentUser.Id)
                     {
                         if (currentUser.UserRole == UserRole.Admin || currentUser.UserRole == UserRole.Office)
                         {
                             // Professional Wish Popup
                             await _dialogService.ShowAlertAsync("Happy Birthday! 🎂", 
                                 $"Dear {person.FirstName},\n\n" +
                                 "Wishing you a fantastic birthday filled with success and happiness.\n" +
                                 "Thank you for your hard work and dedication!\n\n" +
                                 "Best Regards,\n OCC Management");
                             
                             // Mark as shown for this year
                             prefsService.Preferences.LastBirthdayWishYear = DateTime.Now.Year;
                             prefsService.SavePreferences();
                         }
                     }
                 }
                 
                 // Send General Notifications for valid birthdays
                 // Make sure loop above didn't block.
                 // We'll just populate the Notification Center
                 var names = string.Join(", ", birthdayPeople.Select(b => b.FirstName));
                 if (!string.IsNullOrEmpty(names))
                 {
                     NotificationVM.AddSystemNotification("Birthdays", $"Happy Birthday to: {names} 🎂");
                 }
             }
             catch (Exception ex)
             {
                 // Ignore
                 System.Diagnostics.Debug.WriteLine($"Birthday check failed: {ex.Message}");
             }
        }

        private async void OnSessionWarning(object? sender, EventArgs e)
        {
            var result = await _dialogService.ShowSessionTimeoutAsync();
            if (!result)
            {
                // User didn't click "Yes" (Timed out or closed)
                PerformLogout();
            }
            // If result is true, UserActivityService handles the "Active" logic via input detection.
        }

        private void OnSessionExpired(object? sender, EventArgs e)
        {
            // Failsafe if dialog didn't close or wasn't shown
            PerformLogout();
        }

        private void PerformLogout()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                // 1. Clear Auth
                await _authService.LogoutAsync(); 
                
                // 2. Stop SignalR
                // _signalRService.StopAsync(); // Optional, but good practice

                // 3. Navigate to Login via proper Navigation Message
                var loginVM = _serviceProvider.GetRequiredService<LoginViewModel>();
                WeakReferenceMessenger.Default.Send(new NavigationMessage(loginVM));
            });
        }


        [RelayCommand]
        public void Exit()
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        [RelayCommand]
        public async Task About()
        {
             await _dialogService.ShowAlertAsync("About ", 
                 "Orange Circle Construction\n\n" +
                 "Version: 1.6.0 (Beta)\n" +
                 "Developed by: Origize63\n\n" +
                 "© 2026 Orange Circle Construction");
        }

        [RelayCommand]
        public async Task ReportBug()
        {
            // Small delay to ensure button click visual feedback finishes
            await Task.Delay(100);
            
            string? screenshotBase64 = null;
            try
            {
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime && lifetime.MainWindow != null)
                {
                    // Find the top-most visible window (likely the one with the bug)
                    var window = lifetime.Windows.LastOrDefault(w => w.IsVisible && w.GetType().Name != "BugReportDialog") ?? lifetime.MainWindow;
                    
                    // Capture screenshot
                    var size = window.Bounds.Size;
                    if (size.Width > 0 && size.Height > 0)
                    {
                        var pixelSize = new PixelSize((int)size.Width, (int)size.Height);
                        using (var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(pixelSize, new Vector(96, 96)))
                        {
                            bitmap.Render(window);
                            using (var ms = new System.IO.MemoryStream())
                            {
                                bitmap.Save(ms);
                                screenshotBase64 = Convert.ToBase64String(ms.ToArray());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error capturing screenshot: {ex.Message}");
            }

            var viewName = "Unknown";

            // 2. Determine View Name via Reflection
            viewName = "Unknown";

            // Check Shell Overlays first
            if (IsProfileOpen)
            {
               viewName = "ProfileView";
            }
            else if (IsNotificationOpen)
            {
               viewName = "NotificationView";
            }
            else if (CurrentWorkspace?.ViewModel != null)
            {
               viewName = GetActiveViewName(CurrentWorkspace.ViewModel);
            }
            
            await _dialogService.ShowBugReportAsync(viewName, screenshotBase64);
        }

        [RelayCommand]
        public async Task TestBirthday()
        {
             var currentUser = _authService.CurrentUser;
             var name = currentUser?.FirstName ?? "User";
             
             // Professional Wish Popup Simulation
             await _dialogService.ShowAlertAsync("Happy Birthday! 🎂", 
                 $"Dear {name},\n\n" +
                 "Wishing you a fantastic birthday filled with success and happiness.\n" +
                 "Thank you for your hard work and dedication!\n\n" +
                 "Best Regards,\n OCC Management");
                 
             // Also simulate notification
             NotificationVM.AddSystemNotification("Birthdays", $"Happy Birthday to: {name} 🎂");
        }


        private string GetActiveViewName(object viewModel)
        {
            if (viewModel == null) return "Unknown";

            // Start with current
            var vmType = viewModel.GetType();
            string currentName = vmType.Name.Replace("ViewModel", "View");

            // 1. Check for Visible Popups/Drawers (Boolean + ViewModel pair) FIRST
            // These are overlays on top of the 'CurrentView', so they should have priority.
            var boolProps = vmType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  .Where(p => p.PropertyType == typeof(bool));

            // Sort so that properties containing "Popup", "Dialog", or "Detail" are checked FIRST
            var sortedProps = boolProps.OrderByDescending(p => p.Name.Contains("Popup") || p.Name.Contains("Dialog") || p.Name.Contains("Detail"));

            foreach (var prop in sortedProps)
            {
                // Only consider properties that contain "Visible", "Open", or "Show" AND are True
                if ((prop.Name.Contains("Visible") || prop.Name.Contains("Open") || prop.Name.Contains("Show")) && 
                    prop.CanRead && (bool)prop.GetValue(viewModel)!)
                {
                    // Exclude common state flags
                    if (prop.Name == "IsBusy" || prop.Name == "IsLoading" || prop.Name == "IsAuthenticated") continue;

                    // Heuristic: Extract base name
                    var baseName = prop.Name.Replace("Is", "").Replace("Visible", "").Replace("Open", "").Replace("Show", "");

                    // Look for the ViewModel property
                    // Added "Selected..." patterns to better support drawers/details
                    var possibleNames = new[] { 
                        baseName, 
                        baseName + "ViewModel", 
                        baseName + "VM",
                        baseName + "Popup",
                        "Selected" + baseName + "VM",
                        "Selected" + baseName + "ViewModel",
                        "Selected" + baseName
                    };

                    foreach (var name in possibleNames)
                    {
                        var vmProp = vmType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (vmProp != null && vmProp.GetValue(viewModel) is object subVm)
                        {
                            // If found, recurse into it
                            return GetActiveViewName(subVm);
                        }
                    }

                    // If no VM property found, ONLY return if it explicitly looks like a popup name
                    if (prop.Name.Contains("Popup") || prop.Name.Contains("Dialog") || prop.Name.Contains("Modal") || prop.Name.Contains("Detail"))
                    {
                        return $"{currentName} ({baseName})";
                    }
                }
            }

            // 2. Check for 'CurrentView' (Sub-navigation) LAST (as it's the background)
            var currentViewProp = vmType.GetProperty("CurrentView");
            if (currentViewProp != null)
            {
                var val = currentViewProp.GetValue(viewModel);
                if (val != null) return GetActiveViewName(val);
            }

            return currentName;
        }



        [RelayCommand]
        public void UploadLogs()
        {
            // Fire and forget - service handles UI updates via Messenger
            var service = _serviceProvider.GetRequiredService<ILogUploadService>();
            _ = service.UploadLogsAsync();
        }


        #endregion

        #region Helper Methods

        private void SideMenu_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SideMenuViewModel.ActiveSection) && !_isSyncingSideMenu)
            {
                NavigateTo(SideMenuViewModel.ActiveSection);
            }
        }

        [RelayCommand]
        public void CloseWorkspace(WorkspaceViewModel workspace)
        {
            if (Workspaces.Contains(workspace))
            {
                Workspaces.Remove(workspace);
                if (Workspaces.Any())
                {
                    CurrentWorkspace = Workspaces.Last();
                }
                else
                {
                    CurrentWorkspace = null!;
                    NavigateTo(Infrastructure.NavigationRoutes.Home);
                }
            }
        }

        [RelayCommand]
        public void CloseAllWorkspaces()
        {
            Workspaces.Clear();
            CurrentWorkspace = null!;
            NavigateTo(Infrastructure.NavigationRoutes.Home);
        }

        [RelayCommand]
        public void CloseOtherWorkspaces(WorkspaceViewModel workspace)
        {
            var target = workspace ?? CurrentWorkspace;
            if (target != null)
            {
                var others = Workspaces.Where(w => w != target).ToList();
                foreach (var other in others)
                {
                    Workspaces.Remove(other);
                }
                CurrentWorkspace = target;
            }
        }

        [RelayCommand]
        public void SetActiveWorkspace(WorkspaceViewModel workspace)
        {
            CurrentWorkspace = workspace;
        }

        private void AddOrActivateWorkspace(string title, string id, object? iconData, ViewModelBase vm)
        {
            var existing = Workspaces.FirstOrDefault(w => w.Id == id);
            if (existing != null)
            {
                CurrentWorkspace = existing;
            }
            else
            {
                var newWorkspace = new WorkspaceViewModel(title, id, iconData ?? "IconDashboard", vm);
                Workspaces.Add(newWorkspace);
                CurrentWorkspace = newWorkspace;
            }
        }

        private object? GetResource(string key)
        {
            if (Application.Current != null && Application.Current.TryGetResource(key, null, out var resource))
            {
                return resource;
            }
            return null;
        }

        private void NavigateTo(string section)
        {
            // Close notification popup when navigating
            IsNotificationOpen = false;

            // Store previous section if it's not a 'temporary' overlay view
            if (_currentSection != "UserPreferences" && _currentSection != "Help")
            {
                _previousSection = _currentSection;
            }
            _currentSection = section;

            if (!_permissionService.CanAccess(section))
            {
                if (section == NavigationRoutes.Home) return; // Should always have home, but safety check

                var deniedVM = _serviceProvider.GetRequiredService<AccessDeniedViewModel>();
                deniedVM.CloseRequested += (s, e) => NavigateTo(_previousSection);
                // Access denied usually replaces content, let's just show it in current workspace or new 'Error' workspace?
                // Let's make it a workspace for now
                AddOrActivateWorkspace("Access Denied", "Error", "IconAlert", deniedVM);
                return;
            }

            ViewModelBase vm = _serviceProvider.GetRequiredService<HomeViewModel>();
            string title = "Home";
            object? icon = GetResource("IconHome");
            string id = section;

            switch (section)
            {
                case NavigationRoutes.Home:
                     vm = _serviceProvider.GetRequiredService<HomeViewModel>();
                     title = "Home";
                     icon = GetResource("IconHome");
                    break;
                case NavigationRoutes.StaffManagement:
                    vm = _serviceProvider.GetRequiredService<EmployeeManagementViewModel>();
                    title = "Employees";
                    icon = GetResource("IconTeam");
                    break;
                case NavigationRoutes.Customers:
                    vm = _serviceProvider.GetRequiredService<CustomerManagementViewModel>();
                    title = "Customers";
                    icon = GetResource("IconPortfolio");
                    break;
                case NavigationRoutes.Projects:
                    vm = _serviceProvider.GetRequiredService<ProjectsViewModel>();
                    title = "Projects";
                    icon = GetResource("IconPortfolio");
                    break;
                case NavigationRoutes.Time:
                    vm = _serviceProvider.GetRequiredService<TimeAttendanceViewModel>();
                    title = "Time & Attendance";
                    icon = GetResource("IconTime");
                    break;
                case NavigationRoutes.Calendar: 
                    vm = _serviceProvider.GetRequiredService<CalendarHubViewModel>();
                    title = "Global Calendar";
                    icon = GetResource("IconCalendar");
                    break;
                case NavigationRoutes.UserManagement:
                    vm = _serviceProvider.GetRequiredService<UserManagementViewModel>();
                    title = "Users";
                    icon = GetResource("IconUserManagement");
                    break;
                case NavigationRoutes.Feature_Wages:
                    vm = _serviceProvider.GetRequiredService<WagesViewModel>();
                    title = "Wages Hub";
                    icon = GetResource("IconWagesDollar");
                    break;
                case "MyProfile":
                    Receive(new OpenProfileMessage());
                    return; // Profile is an overlay, not a workspace
                case NavigationRoutes.HealthSafety:
                    vm = _serviceProvider.GetRequiredService<HealthSafetyViewModel>();
                    title = "HSEQ";
                    icon = GetResource("IconHealthSafety");
                    break;
                case NavigationRoutes.Feature_OrderManagement:
                    vm = _serviceProvider.GetRequiredService<OrderViewModel>();
                    title = "Orders";
                    icon = GetResource("IconCart");
                    break;
                case "OrderList":
                case "Inventory":
                case "ItemList":
                case "Suppliers":
                case NavigationRoutes.Receiving:
                    var orderVM = _serviceProvider.GetRequiredService<OrderViewModel>();
                    orderVM.SetTab(section);
                    vm = orderVM;
                    title = "Orders"; // Same workspace, just different tab
                    id = NavigationRoutes.Feature_OrderManagement; // Reuse same ID to just switch tab
                    icon = GetResource("IconCart");
                    break;
                case "CreateOrder":
                    id = "CreateOrder";
                    var existingCreateOrder = Workspaces.FirstOrDefault(w => w.Id == id);
                    if (existingCreateOrder != null)
                    {
                        if (existingCreateOrder.ViewModel is CreateOrderViewModel existingVM)
                        {
                            _ = existingVM.LoadData();
                        }
                        CurrentWorkspace = existingCreateOrder;
                        return; 
                    }
                    var createOrderVM = _serviceProvider.GetRequiredService<CreateOrderViewModel>();
                    createOrderVM.CloseRequested += (s, e) => NavigateTo(_previousSection);
                    _ = createOrderVM.LoadData();
                    vm = createOrderVM;
                    title = "New Order";
                    icon = GetResource("IconPlus");
                    break;
                case "RestockReview":
                    var restockVM = _serviceProvider.GetRequiredService<RestockReviewViewModel>();
                    _ = restockVM.LoadData();
                    vm = restockVM;
                    title = "Restock Review";
                    icon = GetResource("IconList");
                    break;
                case "ReleaseNotes":
                case "Help":
                     var releaseNotesVM = new OCC.Client.ViewModels.Help.ReleaseNotesViewModel();
                     releaseNotesVM.CloseRequested += (s, e) => NavigateTo(Infrastructure.NavigationRoutes.Home);
                     vm = releaseNotesVM;
                     title = "Release Notes";
                     icon = GetResource("IconHelp");
                     break;
                case NavigationRoutes.AuditLog:
                    vm = _serviceProvider.GetRequiredService<AuditLogViewModel>();
                    title = "Audit Log";
                    icon = GetResource("IconAudit");
                    break;
                case NavigationRoutes.CompanySettings:
                    vm = _serviceProvider.GetRequiredService<CompanySettingsViewModel>();
                    title = "System Settings";
                    icon = GetResource("IconSettings");
                    break;
                case NavigationRoutes.CompanyProfile:
                    vm = _serviceProvider.GetRequiredService<CompanyProfileViewModel>();
                    title = "Company Profile";
                    icon = GetResource("IconCompany");
                    break;
                case NavigationRoutes.UserPreferences:
                    var userPrefsVM = _serviceProvider.GetRequiredService<UserPreferencesViewModel>();
                    userPrefsVM.CloseRequested += (s, e) => NavigateTo(_previousSection);
                    vm = userPrefsVM;
                    title = "Preferences";
                    icon = GetResource("IconGear");
                    break;
                case NavigationRoutes.Feature_BugReports:
                    {
                        var email = _authService.CurrentUser?.Email?.ToLowerInvariant();
                        if (email == "neil@mdk.co.za") 
                        {
                            vm = _serviceProvider.GetRequiredService<BugListViewModel>();
                            title = "Bug Hub (Dev)";
                        }
                        else 
                        {
                            vm = _serviceProvider.GetRequiredService<SupportCenterViewModel>();
                            title = "Support Center";
                        }
                    }
                    icon = GetResource("IconBug");
                    break;
                case NavigationRoutes.Developer:
                    vm = _serviceProvider.GetRequiredService<ViewModels.Developer.DeveloperViewModel>();
                    title = "Developer";
                    icon = GetResource("IconM");
                    break;
                case NavigationRoutes.SupportCenter:
                    vm = _serviceProvider.GetRequiredService<SupportCenterViewModel>();
                    title = "Support Center";
                    icon = GetResource("IconBug");
                    break;
                 default:
                    vm = _serviceProvider.GetRequiredService<HomeViewModel>();
                    title = "Home";
                    icon = GetResource("IconHome");
                    break;
            }
            
            AddOrActivateWorkspace(title, id, icon, vm);
        }

        public void Receive(OpenNotificationsMessage message)
        {
            // Toggle visibility
            IsNotificationOpen = !IsNotificationOpen;
        }

        private void HandleNavigationRequest(string route, object? payload)
        {
            if (route == "InventoryDetail_Create")
            {
                // Open Item Detail as a new Workspace
                var vm = _serviceProvider.GetRequiredService<ItemDetailViewModel>();
                
                // Setup for creation with pre-filled SKU/Name
                if (payload is string searchTerm)
                {
                    var isSku = searchTerm.Any(char.IsDigit) && searchTerm.Any(char.IsUpper);
                    
                    vm.Load(null); // Clear/New
                    if (isSku) vm.Sku = searchTerm;
                    else vm.Description = searchTerm;
                }
                
                AddOrActivateWorkspace("New Item", "CreateItem_" + Guid.NewGuid(), GetResource("IconPlus"), vm);
            }
            else
            {
                NavigateTo(route);
            }
        }

        public void Receive(OpenManageUsersMessage message)
        {
            NavigateTo("UserManagement");
            
            if (message.Value.HasValue && CurrentWorkspace.ViewModel is UserManagementViewModel vm)
            {
                vm.OpenUser(message.Value.Value);
            }
        }

        public async void Receive(TestBirthdayMessage message)
        {
            await TestBirthday();
        }

        public void Receive(ToastNotificationMessage message)
        {
            var toast = message.Value;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                Toasts.Add(toast);
                
                // Auto-fade and remove after 10 seconds
                Task.Run(async () => {
                    await Task.Delay(8000); // Wait 8s then fade
                    for(int i=0; i<20; i++) {
                        await Task.Delay(100);
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => toast.Opacity -= 0.05);
                    }
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Toasts.Remove(toast));
                });
            });
        }

        public void Receive(OpenProfileMessage message)
        {
            CurrentProfile = new Shared.ProfileViewModel(_authService, _projectRepo, _dialogService);
            CurrentProfile.CloseRequested += (s, e) => 
            {
                IsProfileOpen = false;
                CurrentProfile = null;
            };
            CurrentProfile.ChangeEmailRequested += (s, e) => 
            {
                 // Handling email change if critical, for now ProfileViewModel handles its own logic or alerts
            };
            IsProfileOpen = true;
        }

        public void Receive(OpenBugReportMessage message)
        {
            NavigateTo(Infrastructure.NavigationRoutes.Feature_BugReports);
        }

        public void Receive(ProjectSettingsRequestedMessage message)
        {
            // Navigate to Company Settings (where project settings reside or are managed)
            NavigateTo(Infrastructure.NavigationRoutes.CompanySettings);
            
            // If CompanySettingsViewModel has a way to focus on a project, we could pass the ID.
            // For now, navigating to the settings section is a significant improvement over "doing nothing".
        }

        [RelayCommand]
        public void CloseProfile()
        {
            IsProfileOpen = false;
            CurrentProfile = null;
        }

        [RelayCommand]
        public void CloseNotifications()
        {
            IsNotificationOpen = false;
        }

        private void OnUserUiUpdate(System.Collections.Generic.List<OCC.Shared.DTOs.UserConnectionInfo> users)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Check for new connections or disconnections to show toasts
                var currentNames = ConnectedUsers.Select(u => u.Name).ToList();
                var newNames = users.DistinctBy(u => u.UserName).Select(u => u.UserName).ToList();

                // New users
                var joined = newNames.Where(n => !currentNames.Contains(n)).ToList();
                foreach(var name in joined)
                {
                    if (name != _authService.CurrentUser?.Email) // Don't toast for self
                        _toastService.ShowInfo("User Online", $"{name} has connected.");
                }

                // Disconnected users
                var left = currentNames.Where(n => !newNames.Contains(n)).ToList();
                foreach(var name in left)
                {
                    _toastService.ShowWarning("User Offline", $"{name} has disconnected.");
                }

                ConnectedUsers.Clear();
                // Filter distinct users by UserName to avoid duplicates from multiple connections
                var distinctUsers = users.DistinctBy(u => u.UserName).OrderBy(u => u.UserName).ToList();
                
                foreach (var u in distinctUsers) 
                {
                    var timeOnline = DateTime.UtcNow - u.ConnectedAt;
                    var timeStr = timeOnline.TotalMinutes < 1 ? "Just now" : 
                                  timeOnline.TotalHours < 1 ? $"{timeOnline.Minutes}m" : 
                                  $"{timeOnline.Hours}h {timeOnline.Minutes}m";

                    var display = new UserDisplayModel(
                        u.UserName, 
                        timeStr, 
                        u.Status == "Away" ? Avalonia.Media.Brushes.Orange : Avalonia.Media.Brushes.Green
                    );
                    ConnectedUsers.Add(display);
                }
                OnlineCount = distinctUsers.Count;
            });
        }
        
        public record UserDisplayModel(string Name, string TimeOnline, Avalonia.Media.IBrush StatusColor);

        #endregion
        public void Receive(CreateNewTaskMessage message)
        {
            // Instead of navigating, we open the overlay globally
            OpenNewTaskPopup(message.ProjectId, message.InitialDate);
        }

        private async void OpenNewTaskPopup(Guid? projectId = null, DateTime? initialDate = null)
        {
            CurrentTaskDetail = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
            CurrentTaskDetail.CloseRequested += (s, e) => CloseTaskDetail();
            await CurrentTaskDetail.InitializeForCreation(projectId, null, initialDate);
            IsTaskDetailVisible = true;
        }

        [RelayCommand]
        private void CloseTaskDetail()
        {
            IsTaskDetailVisible = false;
            CurrentTaskDetail = null;
        }
    }
}
