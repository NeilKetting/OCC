using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Features.ProjectHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Models;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System.Collections.ObjectModel;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    /// <summary>
    /// The MainViewModel serves as the application shell view model for the WPF Client.
    /// It coordinates navigation, multi-tab (Hubs) management, real-time communications (SignalR),
    /// database health polling, user activity/inactivity session monitoring, and app-wide toasts/messages.
    /// </summary>
    public partial class MainViewModel : ViewModelBase, IDisposable, IRecipient<ToastNotificationMessage>, IRecipient<CloseHubMessage>, IRecipient<OpenHubMessage>, IRecipient<OpenProjectMessage>, IRecipient<StatusUpdateMessage>, IRecipient<PreferenceChangedMessage>, IRecipient<ImportProgressMessage>
    {
        #region Private Fields & Services

        // --- Injected Services & Dependencies ---
        private readonly ILogger<MainViewModel> _logger;
        
        // Controls user access permissions to specific UI routes/features
        private readonly IPermissionService _permissionService;             
        
        // Manages user authentication and current session user details
        private readonly IAuthService _authService;                         
        
        // Handles real-time client-server connection status and online users list
        private readonly ISignalRService _signalRService;                   
        
        // DI container to resolve child view models on-demand
        private readonly IServiceProvider _serviceProvider;                 
        
        // Retrieves navigation items configuration
        private readonly IFeatureService _featureService;                   
        
        // Monitors user inactivity, session warning, and session timeouts
        private readonly UserActivityService _userActivityService;         
        
        // Periodically checks connectivity to the database
        private readonly IDatabaseStatusService _databaseStatusService;     
        
        // Handles timed transitions like auto-fading toast notifications
        private readonly IShellTimingService _shellTimingService;           
        
        // Stores configuration for environments (e.g. Local, Production)
        private readonly ConnectionSettings _connectionSettings;           
        
        // Manages user preferences (like menu icon style)
        private readonly LocalSettingsService _localSettings;               
        
        // Checks for and installs application updates
        private readonly IUpdateService _updateService;                     
        
        // Displays message dialogs, alerts, and confirmation boxes
        private readonly IDialogService _dialogService;                     
        
        // --- Background Tasks & Cancellation ---
        
        // Controls cancellation of the database polling loop
        private CancellationTokenSource? _dbPollingCts;                     
        
        // Controls cancellation of async shell-timing tasks (toasts/status updates)
        private CancellationTokenSource? _shellTimingCts;                   
        
        // Reference to the running database connection check task
        private Task? _dbPollingTask;                                       

        // --- Timers & Backing Fields ---
        
        // Timer to tick every second for the clock display
        private readonly System.Windows.Threading.DispatcherTimer _clockTimer; 
        
        // Backing field for the currently active tab/hub
        private ViewModelBase? _activeHub;                                  

        #endregion

        #region Properties & Observables

        // --- Navigation & Identity Observables ---
        [ObservableProperty]
        private INavigationService _navigation;

        [ObservableProperty]
        private string _userName = string.Empty;

        [ObservableProperty]
        private string _userEmail = string.Empty;

        [ObservableProperty]
        private string _userInitials = "??";

        [ObservableProperty]
        private ObservableCollection<NavItem> _navigationItems = new();

        // --- Hubs (Multi-Tab UI) & Busy States ---
        [ObservableProperty]
        private ObservableCollection<ViewModelBase> _openHubs = new();

        [ObservableProperty]
        private bool _isAppBusy;

        [ObservableProperty]
        private string _busyMessage = "Please wait...";

        // --- Search & Sidebar UI ---
        [ObservableProperty]
        private string _featureSearchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<NavItem> _filteredNavigationItems = new();

        // --- Overlays (Modals & Bug Reports) ---
        [ObservableProperty]
        private ViewModelBase? _currentReportBug;

        [ObservableProperty]
        private ViewModelBase? _currentProfile;

        [ObservableProperty]
        private bool _isAboutVisible;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        public bool UsePlainMenuIcons => _localSettings.Settings.UsePlainMenuIcons;

        // --- Toast Alerts ---
        public ObservableCollection<ToastMessage> Toasts { get; } = new();

        // --- Active Hub Property ---
        /// <summary>
        /// Gets or sets the currently selected / active hub (tab) in the main UI shell.
        /// Automatically handles registering property changed events to map busy states to the main window.
        /// </summary>
        public ViewModelBase? ActiveHub
        {
            get => _activeHub;
            set
            {
                var oldHub = _activeHub;
                if (SetProperty(ref _activeHub, value))
                {
                    // Unsubscribe busy status notifications from the old active tab
                    if (oldHub != null)
                    {
                        oldHub.PropertyChanged -= OnActiveHubPropertyChanged;
                    }

                    // Subscribe to busy status notifications from the new active tab
                    if (_activeHub != null)
                    {
                        _activeHub.PropertyChanged += OnActiveHubPropertyChanged;
                        UpdateBusyState();
                    }

                    // Toggle IsActiveHub state across all tabs (useful for triggering refresh or activation events)
                    foreach (var hub in OpenHubs)
                    {
                        hub.IsActiveHub = (hub == value);
                    }
                }
            }
        }

        /// <summary>
        /// Relays busy state changes from the active tab up to the main application shell's busy overlay.
        /// </summary>
        private void OnActiveHubPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModelBase.IsBusy) || e.PropertyName == nameof(ViewModelBase.BusyText))
            {
                UpdateBusyState();
            }
        }

        /// <summary>
        /// Synchronizes the Busy status variables with the active hub.
        /// </summary>
        private void UpdateBusyState()
        {
            if (ActiveHub != null)
            {
                IsAppBusy = ActiveHub.IsBusy;
                BusyMessage = ActiveHub.BusyText;
            }
            else
            {
                IsAppBusy = false;
            }
        }

        [ObservableProperty]
        private bool _isSidebarMinimized = true;

        [ObservableProperty]
        private string _userActivityStatus = "Active";

        // --- Permission Helper Properties for Menu UI Bindings ---
        // These query the PermissionService to decide which main menu options should be visible/enabled.
        
        public bool CanAccessChat => _permissionService.CanAccess(NavigationRoutes.Chat);
        public bool CanAccessStaff => _permissionService.CanAccess(NavigationRoutes.StaffManagement);
        public bool CanAccessAttendance => _permissionService.CanAccess(NavigationRoutes.AttendanceLive);
        public bool CanAccessProjects => _permissionService.CanAccess(NavigationRoutes.Projects);
        public bool CanAccessCustomers => _permissionService.CanAccess(NavigationRoutes.Customers);
        public bool CanAccessInventory => _permissionService.CanAccess(NavigationRoutes.Inventory);
        public bool CanAccessProcurement => _permissionService.CanAccess(NavigationRoutes.Procurement);
        public bool CanAccessPurchaseOrders => _permissionService.CanAccess(NavigationRoutes.PurchaseOrder);
        public bool CanAccessSuppliers => _permissionService.CanAccess(NavigationRoutes.Suppliers);
        public bool CanAccessHealthSafety => _permissionService.CanAccess(NavigationRoutes.HealthSafety);
        
        // Partner Hub grouping permissions
        public bool CanAccessPartnerHub => _permissionService.CanAccess("Partners") || CanAccessSubContractors || CanAccessSnagList || CanAccessPerformanceDashboard;
        public bool CanAccessSubContractors => _permissionService.CanAccess(NavigationRoutes.SubContractors);
        public bool CanAccessSnagList => _permissionService.CanAccess(NavigationRoutes.SnagList);
        public bool CanAccessPerformanceDashboard => _permissionService.CanAccess(NavigationRoutes.PerformanceDashboard);

        // Admin functionality permissions
        public bool CanAccessUserManagement => _permissionService.CanAccess(NavigationRoutes.UserManagement);
        public bool CanAccessAuditLog => _permissionService.CanAccess(NavigationRoutes.AuditLog);
        public bool CanAccessCompanyProfile => _permissionService.CanAccess(NavigationRoutes.CompanyProfile);
        public bool CanAccessSettings => _permissionService.CanAccess(NavigationRoutes.CompanySettings);

        public bool CanAccessAdmin => CanAccessUserManagement || CanAccessAuditLog || CanAccessCompanyProfile || CanAccessSettings;

        // --- Status Bar & Connection Observables ---
        [ObservableProperty]
        private bool _isUserInactive;

        [ObservableProperty]
        private string _dbStatusText = "Checking...";

        [ObservableProperty]
        private string _environmentName = "PRODUCTION";

        [ObservableProperty]
        private string _databaseName = "LIVE";

        [ObservableProperty]
        private bool _isDbConnected = true;

        // --- Real-time Session & Online Users (SignalR) ---
        [ObservableProperty]
        private string _onlineCount = "0";

        [ObservableProperty]
        private ObservableCollection<UserDisplayModel> _connectedUsers = new();

        // --- Clock & Dates ---
        [ObservableProperty]
        private string _currentTime = string.Empty;

        [ObservableProperty]
        private string _currentDate = string.Empty;

        // --- Header Menu Visual States ---
        [ObservableProperty]
        private bool _isUserListVisible;
        
        [ObservableProperty]
        private bool _isProfileMenuVisible;

        [ObservableProperty]
        private string _statusMessage = "Ready";
        
        // --- Data Import Tracking ---
        [ObservableProperty]
        private double _importProgress;
        
        [ObservableProperty]
        private bool _isImportProgressVisible;
        
        [ObservableProperty]
        private string _importProgressText = string.Empty;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the main application shell's view model. Sets up injected services,
        /// registers message handlers, starts SignalR, user activity monitors, the clock, and DB polling.
        /// </summary>
        public MainViewModel(
            ILogger<MainViewModel> logger,
            INavigationService navigation, 
            IPermissionService permissionService, 
            IAuthService authService, 
            ISignalRService signalRService, 
            IServiceProvider serviceProvider, 
            IFeatureService featureService,
            UserActivityService userActivityService,
            IDatabaseStatusService databaseStatusService,
            IShellTimingService shellTimingService,
            ConnectionSettings connectionSettings,
            IUpdateService updateService,
            IDialogService dialogService)
        {
            _logger = logger;
            _navigation = navigation;
            _permissionService = permissionService;
            _authService = authService;
            _signalRService = signalRService;
            _serviceProvider = serviceProvider;
            _featureService = featureService;
            _userActivityService = userActivityService;
            _databaseStatusService = databaseStatusService;
            _shellTimingService = shellTimingService;
            _connectionSettings = connectionSettings;
            _updateService = updateService;
            _dialogService = dialogService;
            _localSettings = _serviceProvider.GetRequiredService<LocalSettingsService>();

            // Map current logged-in user profile details
            if (_authService.CurrentUser != null)
            {
                UserName = $"{_authService.CurrentUser.FirstName} {_authService.CurrentUser.LastName}";
                UserEmail = _authService.CurrentUser.Email;
                
                var first = _authService.CurrentUser.FirstName?.FirstOrDefault() ?? '?';
                var last = _authService.CurrentUser.LastName?.FirstOrDefault() ?? '?';
                UserInitials = $"{first}{last}".ToUpper();
            }

            Title = "Main Shell";
            AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            
            // Start navigation sidebar minimized by default
            IsSidebarMinimized = true;

            // Load and filter navigation routes based on user permission roles
            InitializeNavigation();
            UpdateFilteredNavigationItems();
            
            // Setup CollectionView filtering/grouping for structured navigation categories
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(NavigationItems);
            view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(NavItem.Category)));

            // Setup clock update timer
            _clockTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => UpdateTime();
            _clockTimer.Start();
            UpdateTime(); // initial call

            // Register message handlers via the CommunityToolkit WeakReferenceMessenger
            WeakReferenceMessenger.Default.Register<ToastNotificationMessage>(this);
            WeakReferenceMessenger.Default.Register<CloseHubMessage>(this);
            WeakReferenceMessenger.Default.Register<OpenHubMessage>(this);
            WeakReferenceMessenger.Default.Register<OpenProjectMessage>(this);
            WeakReferenceMessenger.Default.Register<StatusUpdateMessage>(this);
            WeakReferenceMessenger.Default.Register<PreferenceChangedMessage>(this);
            WeakReferenceMessenger.Default.Register<ImportProgressMessage>(this);
            
            // Connect to real-time SignalR notifications and user lists
            _signalRService.UserListUpdated += OnUserListUpdated;
            _ = _signalRService.StartAsync();

            // Setup User Activity Monitoring & Session auto-expiry timers
            UserActivityStatus = _userActivityService.StatusText;
            IsUserInactive = _userActivityService.IsAway;
            
            _userActivityService.PropertyChanged += OnUserActivityPropertyChanged;
            _userActivityService.SessionExpired += OnSessionExpired;
            _userActivityService.SessionWarning += OnSessionWarning;

            // Initialize Environment Settings
            EnvironmentName = _connectionSettings.SelectedEnvironment.ToString().ToUpper();
            DatabaseName = "CONNECTING...";

            // Start Database connection health check loop in the background
            _dbPollingCts = new CancellationTokenSource();
            _shellTimingCts = new CancellationTokenSource();
            _dbPollingTask = StartDbPollingAsync(_dbPollingCts.Token);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Shows/hides the list of currently connected users in the header bar.
        /// </summary>
        [RelayCommand]
        private void ToggleUserList()
        {
            IsUserListVisible = !IsUserListVisible;
            if (IsUserListVisible) IsProfileMenuVisible = false;
        }

        /// <summary>
        /// Shows/hides the profile drop-down menu in the header bar.
        /// </summary>
        [RelayCommand]
        private void ToggleProfileMenu()
        {
            IsProfileMenuVisible = !IsProfileMenuVisible;
            if (IsProfileMenuVisible) IsUserListVisible = false;
        }

        /// <summary>
        /// Expands or minimizes the left navigation sidebar menu.
        /// </summary>
        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarMinimized = !IsSidebarMinimized;
        }

        /// <summary>
        /// Resolves and displays the User Profile dialog/overlay.
        /// </summary>
        [RelayCommand]
        private void ShowProfile()
        {
            CurrentProfile = _serviceProvider.GetRequiredService<ProfileViewModel>();
        }

        /// <summary>
        /// Closes the User Profile dialog/overlay.
        /// </summary>
        [RelayCommand]
        private void CloseProfile()
        {
            CurrentProfile = null;
        }

        /// <summary>
        /// Displays the "About" application info dialog/overlay.
        /// </summary>
        [RelayCommand]
        private void ShowAbout()
        {
            IsAboutVisible = true;
        }

        /// <summary>
        /// Closes the "About" application info dialog/overlay.
        /// </summary>
        [RelayCommand]
        private void CloseAbout()
        {
            IsAboutVisible = false;
        }

        /// <summary>
        /// Performs user logout on the backend and navigates the shell back to the Auth screen.
        /// </summary>
        [RelayCommand]
        private async Task Logout()
        {
            await _authService.LogoutAsync();
            Navigation.NavigateTo("Auth");
        }

        /// <summary>
        /// Locates the active overlay/sub-view and opens the Report Bug dialog, pre-populating the view name.
        /// </summary>
        [RelayCommand]
        private void ShowReportBug()
        {
            var currentVM = (ViewModelBase?)ActiveHub;
            
            // Recursively find the topmost active overlay or nested view (e.g. CurrentView)
            while (currentVM != null)
            {
                if (currentVM is IOverlayProvider overlayProvider && overlayProvider.ActiveOverlay != null)
                {
                    currentVM = overlayProvider.ActiveOverlay;
                    continue;
                }

                var currentViewProp = currentVM.GetType().GetProperty("CurrentView");
                if (currentViewProp != null)
                {
                    var nestedVM = currentViewProp.GetValue(currentVM) as ViewModelBase;
                    if (nestedVM != null)
                    {
                        currentVM = nestedVM;
                        continue;
                    }
                }

                break;
            }

            var viewName = currentVM?.GetType().Name.Replace("ViewModel", "View") ?? "Main Shell";
            if (viewName == "TeamManagementView")
            {
                viewName = "TeamManagementListView";
            }
            var viewModelType = Navigation.GetViewModelTypeForRoute("Support.ReportBug");
            if (viewModelType == null) return;

            var hub = (ViewModelBase)_serviceProvider.GetRequiredService(viewModelType);
            
            // Use dynamic cast to call Initialize since ReportBugViewModel might be in a different module
            (hub as dynamic).Initialize(viewName);
            CurrentReportBug = hub;
        }

        /// <summary>
        /// Opens the general Support Hub tab.
        /// </summary>
        [RelayCommand]
        private void ShowSupportHub()
        {
            OpenHub("Support.SupportHub");
        }

        /// <summary>
        /// Performs updates check using the UpdateService and prompt the user to install updates.
        /// </summary>
        [RelayCommand]
        private async Task CheckForUpdates()
        {
            try
            {
                IsAppBusy = true;
                BusyMessage = "Checking for updates...";
                
                // Add a small artificial delay so the user can see it's actually doing something
                await Task.Delay(1500);

                var update = await _updateService.CheckForUpdatesAsync();
                
                IsAppBusy = false;

                if (update != null)
                {
                    var confirmed = await _dialogService.ShowConfirmationAsync("Update Available", 
                        $"A new version (v{update.TargetFullRelease.Version}) is available. Would you like to download and install it now? The application will restart after downloading.");
                    
                    if (confirmed)
                    {
                        IsAppBusy = true;
                        BusyMessage = "Downloading update...";
                        
                        await _updateService.DownloadUpdatesAsync(update, p => 
                        {
                            BusyMessage = $"Downloading update ({p}%)...";
                        });

                        await _dialogService.ShowAlertAsync("Update Ready", "The update has been downloaded. The application will now restart to apply changes.");
                        _updateService.ApplyUpdatesAndRestart(update);
                    }
                }
                else
                {
                    await _dialogService.ShowAlertAsync("No Updates", "You are already running the latest version of OCC ERP.");
                }
            }
            catch (Exception ex)
            {
                IsAppBusy = false;
                _logger.LogError(ex, "Error checking for updates from main menu");
                await _dialogService.ShowAlertAsync("Update Error", "An error occurred while checking for updates. Please check your internet connection and try again.");
            }
        }

        /// <summary>
        /// Shuts down the current application context.
        /// </summary>
        [RelayCommand]
        private void ExitApp()
        {
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Main navigation entry command. Accepts a NavItem or Route string, verifies access, and opens the hub.
        /// </summary>
        [RelayCommand]
        private void Navigate(object? parameter)
        {
            if (parameter == null) return;
            
            NavItem? item = parameter as NavItem;
            
            // If parameter is string, try to find matching NavItem by route
            if (item == null && parameter is string route)
            {
                item = NavigationItems.FirstOrDefault(n => n.Route == route) 
                       ?? NavigationItems.SelectMany(n => n.Children).FirstOrDefault(n => n.Route == route);
                
                // Fallback: If no NavItem found but we have a route string, handle it directly
                if (item == null)
                {
                    HandleRoute(route);
                    return;
                }
            }

            if (item == null) return;

            // If it's a parent node (dropdown group), just expand/collapse it
            if (item.IsParent)
            {
                item.IsExpanded = !item.IsExpanded;
                return;
            }

            if (string.IsNullOrEmpty(item.Route)) return;
            
            HandleRoute(item.Route);

            // Sync navigation UI active indicators
            foreach (var current in NavigationItems)
            {
                current.IsActive = current == item;
                if (current.IsParent)
                {
                    foreach (var child in current.Children)
                    {
                        child.IsActive = child == item;
                    }
                }
            }
        }

        /// <summary>
        /// Closes a specific active tab/hub and disposes its resources.
        /// </summary>
        [RelayCommand]
        private void CloseHub(ViewModelBase hub)
        {
            // Protect the Dashboard from being closed as it acts as the primary landing page
            if (hub.Title == "Dashboard") return;
            
            if (hub == CurrentReportBug)
            {
                CurrentReportBug = null;
                return;
            }

            hub.Dispose();
            OpenHubs.Remove(hub);
            if (ActiveHub == hub)
            {
                ActiveHub = OpenHubs.LastOrDefault();
            }
        }

        /// <summary>
        /// Sets a specific open tab/hub as the active focus view.
        /// </summary>
        [RelayCommand]
        private void NavigateToHub(ViewModelBase hub)
        {
            ActiveHub = hub;
        }

        /// <summary>
        /// Closes all open tabs except the home Dashboard.
        /// </summary>
        [RelayCommand]
        private void CloseAllTabs()
        {
            foreach (var hub in OpenHubs.ToList())
            {
                if (hub.Title != "Dashboard")
                {
                    hub.Dispose();
                }
            }
            OpenHubs.Clear();
            ActiveHub = null;
        }

        /// <summary>
        /// Closes all open tabs except the target hub and the home Dashboard.
        /// </summary>
        [RelayCommand]
        private void CloseOtherTabs(ViewModelBase currentHub)
        {
            var hubToKeep = currentHub ?? ActiveHub;
            if (hubToKeep == null) return;
            
            var hubsToRemove = OpenHubs.Where(h => h != hubToKeep && h.Title != "Dashboard").ToList();
            foreach (var hub in hubsToRemove)
            {
                hub.Dispose();
                OpenHubs.Remove(hub);
            }
            ActiveHub = hubToKeep;
        }

        /// <summary>
        /// Closes all open tabs positioned to the right of the target tab.
        /// </summary>
        [RelayCommand]
        private void CloseTabsToRight(ViewModelBase currentHub)
        {
            var referenceHub = currentHub ?? ActiveHub;
            if (referenceHub == null) return;
            
            var index = OpenHubs.IndexOf(referenceHub);
            if (index >= 0)
            {
                var hubsToRemove = OpenHubs.Skip(index + 1).ToList();
                foreach (var hub in hubsToRemove)
                {
                    hub.Dispose();
                    OpenHubs.Remove(hub);
                }
            }
            ActiveHub = referenceHub;
        }

        /// <summary>
        /// Manually removes a toast notification from the overlay list.
        /// </summary>
        [RelayCommand]
        private void CloseToast(ToastMessage toast)
        {
            if (toast == null) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() => Toasts.Remove(toast));
        }

        #endregion

        #region Navigation & Routing Logic

        /// <summary>
        /// Fetches the application feature list and populates the navigation list based on user permission level.
        /// </summary>
        private void InitializeNavigation()
        {
            var items = _featureService.GetNavigationItems();

            foreach (var item in items)
            {
                // Top-level permission check
                if (string.IsNullOrEmpty(item.Route) || _permissionService.CanAccess(item.Route))
                {
                    // Filter child menu items by current permissions
                    var accessibleChildren = item.Children.Where(c => string.IsNullOrEmpty(c.Route) || _permissionService.CanAccess(c.Route)).ToList();
                    
                    item.Children.Clear();
                    foreach (var child in accessibleChildren)
                    {
                        item.Children.Add(child);
                    }

                    // Only process if it has children, or if it's a standalone endpoint
                    if (item.IsParent || !string.IsNullOrEmpty(item.Route))
                    {
                        // Check if we already have a parent with this name to prevent duplicate group nodes
                        var existingParent = NavigationItems.FirstOrDefault(i => i.Label == item.Label && i.IsParent);
                        if (existingParent != null)
                        {
                            // Merge children into existing parent
                            foreach (var child in item.Children)
                            {
                                if (!existingParent.Children.Any(c => c.Label == child.Label))
                                {
                                    existingParent.Children.Add(child);
                                }
                            }
                        }
                        else
                        {
                            NavigationItems.Add(item);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Validates permissions for the target route before opening the Hub/Tab.
        /// </summary>
        private void HandleRoute(string route)
        {
            if (string.IsNullOrEmpty(route)) return;

            // Security check to prevent bypass
            if (route != "Support.ReportBug" && !_permissionService.CanAccess(route))
            {
                _logger.LogWarning("Unauthorized access attempt to route: {Route}", route);
                WeakReferenceMessenger.Default.Send(new ToastNotificationMessage(new ToastMessage("Access Denied", "You do not have permission to access this hub.", ToastType.Error)));
                return;
            }

            if (route == "Support.ReportBug")
            {
                ShowReportBug();
                return;
            }

            OpenHub(route);
        }

        /// <summary>
        /// Instantiates or focuses a Hub/ViewModel based on route mapping.
        /// </summary>
        private void OpenHub(string route)
        {
            var viewModelType = Navigation.GetViewModelTypeForRoute(route);
            if (viewModelType == null) return;

            var existing = OpenHubs.FirstOrDefault(h => h.GetType() == viewModelType);
            if (existing != null)
            {
                ActiveHub = existing;
            }
            else
            {
                var hub = (ViewModelBase)_serviceProvider.GetRequiredService(viewModelType);
                OpenHubs.Add(hub);
                ActiveHub = hub;
            }
        }

        #endregion

        #region Live Feature/Menu Filter Logic

        partial void OnFeatureSearchQueryChanged(string value)
        {
            UpdateFilteredNavigationItems();
        }

        /// <summary>
        /// Filters list of visible sidebar navigation items based on FeatureSearchQuery string.
        /// </summary>
        private void UpdateFilteredNavigationItems()
        {
            if (string.IsNullOrWhiteSpace(FeatureSearchQuery))
            {
                FilteredNavigationItems = NavigationItems == null 
                    ? new ObservableCollection<NavItem>() 
                    : new ObservableCollection<NavItem>(NavigationItems);
                return;
            }

            var results = new List<NavItem>();
            var query = FeatureSearchQuery.ToLower();

            foreach (var item in NavigationItems)
            {
                // Check parent node matches query
                bool parentMatches = item.Label.ToLower().Contains(query);
                
                // Check child nodes matching query
                var matchedChildren = item.Children
                    .Where(c => c.Label.ToLower().Contains(query))
                    .ToList();

                if (parentMatches || matchedChildren.Any())
                {
                    // Create a result item that includes the matches
                    var resultItem = new NavItem(item.Label, item.Route, item.Category, iconCode: item.IconCode)
                    {
                        IsExpanded = true
                    };
                    
                    foreach (var child in matchedChildren)
                    {
                        resultItem.Children.Add(child);
                    }
                    
                    results.Add(resultItem);
                }
            }

            FilteredNavigationItems = new ObservableCollection<NavItem>(results);
        }

        #endregion

        #region Database & Connection Status Polling

        /// <summary>
        /// Loops indefinitely in the background to poll database status every 30 seconds.
        /// </summary>
        private async Task StartDbPollingAsync(CancellationToken cancellationToken)
        {
            try
            {
                await CheckDbConnection(cancellationToken);

                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await CheckDbConnection(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the shell is disposed or the user logs out.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database polling stopped unexpectedly.");
                IsDbConnected = false;
                DbStatusText = "Offline: Disconnected";
            }
        }

        /// <summary>
        /// Invokes the DatabaseStatusService to verify connectivity status and database name.
        /// </summary>
        private async Task CheckDbConnection(CancellationToken cancellationToken)
        {
            var status = await _databaseStatusService.CheckAsync(cancellationToken);
            IsDbConnected = status.IsConnected;
            DbStatusText = status.StatusText;
            DatabaseName = status.DatabaseName;
        }

        #endregion

        #region Real-Time Sync (SignalR)

        /// <summary>
        /// Triggered by SignalRService when user connection statuses change. Updates the UI on-thread.
        /// </summary>
        private void OnUserListUpdated(List<OCC.Shared.DTOs.UserConnectionInfo> users)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                ConnectedUsers.Clear();
                foreach (var u in users)
                {
                    var timeOnline = DateTime.UtcNow - u.ConnectedAt;
                    var timeStr = timeOnline.TotalMinutes < 1 ? "Just now" : 
                                  timeOnline.TotalHours < 1 ? $"{(int)timeOnline.TotalMinutes}m" : 
                                  $"{(int)timeOnline.TotalHours}h";

                    ConnectedUsers.Add(new UserDisplayModel(
                        u.UserName ?? "Unknown", 
                        timeStr, 
                        u.Status == "Online" ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Orange));
                }
                OnlineCount = users.Count.ToString();
            });
        }

        /// <summary>
        /// Display model representing a single connected online user in the header user list overlay.
        /// </summary>
        public record UserDisplayModel(string Name, string TimeOnline, System.Windows.Media.Brush StatusColor);

        #endregion

        #region Clock & Date Formatting Helpers

        /// <summary>
        /// Updates the observable current time and formatted date properties. Called on every timer tick.
        /// </summary>
        private void UpdateTime()
        {
            var now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm:ss");
            CurrentDate = now.ToString("dddd, d") + GetDaySuffix(now.Day) + now.ToString(" MMMM yyyy");
        }

        /// <summary>
        /// Helper to append appropriate suffixes (st, nd, rd, th) to calendar day integers.
        /// </summary>
        private string GetDaySuffix(int day)
        {
            switch (day)
            {
                case 1:
                case 21:
                case 31:
                    return "st";
                case 2:
                case 22:
                    return "nd";
                case 3:
                case 23:
                    return "rd";
                default:
                    return "th";
            }
        }

        #endregion

        #region WeakReferenceMessenger Messages Receivers

        /// <summary>
        /// Relays hub open messages.
        /// </summary>
        public void Receive(OpenHubMessage message)
        {
            OpenHub(message.Value);
        }

        /// <summary>
        /// Relays hub close messages.
        /// </summary>
        public void Receive(CloseHubMessage message)
        {
            CloseHub(message.Value);
        }

        /// <summary>
        /// Handles OpenProjectMessages by loading the specific project ID in a ProjectDetailViewModel tab.
        /// </summary>
        public void Receive(OpenProjectMessage message)
        {
            var projectId = message.Value;
            
            // Check if project tab is already open
            var existing = OpenHubs.OfType<ProjectDetailViewModel>().FirstOrDefault(p => p.ProjectId == projectId);
            if (existing != null)
            {
                ActiveHub = existing;
                return;
            }

            var hub = _serviceProvider.GetRequiredService<ProjectDetailViewModel>();
            _ = hub.LoadProjectAsync(projectId);
            OpenHubs.Add(hub);
            ActiveHub = hub;
        }

        /// <summary>
        /// Displays incoming toast alerts and starts the automatic fade-out timer.
        /// </summary>
        public void Receive(ToastNotificationMessage message)
        {
            var toast = message.Value;
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                 Toasts.Add(toast);
            });

            if (!toast.IsSticky)
            {
                _ = _shellTimingService.FadeOutToastAsync(toast, item => Toasts.Remove(item), _shellTimingCts?.Token ?? CancellationToken.None);
            }
        }

        /// <summary>
        /// Relays status bar text updates and handles fading them out back to "Ready".
        /// </summary>
        public void Receive(StatusUpdateMessage message)
        {
            StatusMessage = message.Value;
            
            if (message.Value != "Ready")
            {
                _ = _shellTimingService.ResetStatusAsync(message.Value, () => StatusMessage, value => StatusMessage = value, _shellTimingCts?.Token ?? CancellationToken.None);
            }
        }

        /// <summary>
        /// Displays/updates the UI progress indicator when background imports are active.
        /// </summary>
        public void Receive(ImportProgressMessage message)
        {
            var info = message.Value;
            ImportProgress = info.Progress;
            ImportProgressText = info.Message;
            IsImportProgressVisible = info.IsVisible;
            
            if (info.IsComplete)
            {
                _ = _shellTimingService.HideImportProgressAsync(() => IsImportProgressVisible = false, _shellTimingCts?.Token ?? CancellationToken.None);
            }
        }

        /// <summary>
        /// Observes change of user interface preference changes, e.g. toggling menu icon display types.
        /// </summary>
        public void Receive(PreferenceChangedMessage message)
        {
            if (message.PreferenceName == nameof(LocalSettings.UsePlainMenuIcons))
            {
                OnPropertyChanged(nameof(UsePlainMenuIcons));
            }
        }

        #endregion

        #region User Activity & Session Events

        private void OnUserActivityPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserActivityService.StatusText))
                UserActivityStatus = _userActivityService.StatusText;
            
            if (e.PropertyName == nameof(UserActivityService.IsAway))
                IsUserInactive = _userActivityService.IsAway;
        }

        /// <summary>
        /// Triggered when the user activity service detects session expiration. Automatically logs the user out.
        /// </summary>
        private async void OnSessionExpired(object? s, EventArgs e)
        {
            await App.Current.Dispatcher.Invoke(async () => 
            {
                WeakReferenceMessenger.Default.Send(new ToastNotificationMessage(new ToastMessage("Session Expired", "You have been logged out due to inactivity.", ToastType.Warning)));
                await Logout();
            });
        }

        /// <summary>
        /// Triggered shortly before session expiration to prompt the user to become active.
        /// </summary>
        private void OnSessionWarning(object? s, EventArgs e)
        {
            App.Current.Dispatcher.Invoke(() => 
            {
                // Prevent duplicate inactivity warnings
                if (Toasts.Any(t => t.Title == "Inactivity Warning")) return;

                WeakReferenceMessenger.Default.Send(new ToastNotificationMessage(new ToastMessage("Inactivity Warning", "Your session will expire in 1 minute. Move the mouse to stay logged in.", ToastType.Info)));
            });
        }

        #endregion

        #region Lifecycle & Disposal

        /// <summary>
        /// Safely disposes open view models, unsubscribes from service events, stops clocks/timers,
        /// and cancels background polling loops to prevent memory leaks.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            _logger.LogInformation("Disposing MainViewModel and all open hubs");

            foreach (var hub in OpenHubs.ToList())
            {
                hub.Dispose();
            }
            OpenHubs.Clear();
            
            // Unsubscribe from global services to prevent memory leaks and duplicate event triggers
            if (_signalRService != null)
            {
                _signalRService.UserListUpdated -= OnUserListUpdated;
            }

            if (_userActivityService != null)
            {
                _userActivityService.PropertyChanged -= OnUserActivityPropertyChanged;
                _userActivityService.SessionExpired -= OnSessionExpired;
                _userActivityService.SessionWarning -= OnSessionWarning;
            }

            if (_clockTimer != null)
            {
                _clockTimer.Stop();
            }

            _dbPollingCts?.Cancel();
            _dbPollingCts?.Dispose();
            _dbPollingCts = null;
            _shellTimingCts?.Cancel();
            _shellTimingCts?.Dispose();
            _shellTimingCts = null;
            if (_dbPollingTask?.IsFaulted == true)
            {
                _logger.LogError(_dbPollingTask.Exception, "Database polling faulted during disposal.");
            }
            _dbPollingTask = null;

            // Unregister from Messenger
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        #endregion
    }
}