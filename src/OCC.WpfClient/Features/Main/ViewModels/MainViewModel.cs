using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Models;
using OCC.Shared.DTOs;
using System.Threading.Tasks;
using System.Net.Http;
using OCC.WpfClient.Features.ProjectHub.ViewModels;
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    public partial class MainViewModel : ViewModelBase, IRecipient<ToastNotificationMessage>, IRecipient<CloseHubMessage>, IRecipient<OpenHubMessage>, IRecipient<OpenProjectMessage>, IRecipient<StatusUpdateMessage>
    {
        private readonly IPermissionService _permissionService;
        private readonly IAuthService _authService;
        private readonly ISignalRService _signalRService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IFeatureService _featureService;
        private readonly UserActivityService _userActivityService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConnectionSettings _connectionSettings;

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

        [ObservableProperty]
        private ObservableCollection<ViewModelBase> _openHubs = new();

        [ObservableProperty]
        private bool _isAppBusy;

        [ObservableProperty]
        private string _busyMessage = "Please wait...";

        [ObservableProperty]
        private string _featureSearchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<NavItem> _filteredNavigationItems = new();

        [ObservableProperty]
        private ViewModelBase? _currentReportBug;

        [ObservableProperty]
        private ViewModelBase? _currentProfile;

        [ObservableProperty]
        private bool _isAboutVisible;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        public ObservableCollection<ToastMessage> Toasts { get; } = new();

        private ViewModelBase? _activeHub;
        public ViewModelBase? ActiveHub
        {
            get => _activeHub;
            set
            {
                var oldHub = _activeHub;
                if (SetProperty(ref _activeHub, value))
                {
                    if (oldHub != null)
                    {
                        oldHub.PropertyChanged -= OnActiveHubPropertyChanged;
                    }

                    if (_activeHub != null)
                    {
                        _activeHub.PropertyChanged += OnActiveHubPropertyChanged;
                        UpdateBusyState();
                    }

                    foreach (var hub in OpenHubs)
                    {
                        hub.IsActiveHub = (hub == value);
                    }
                }
            }
        }

        private void OnActiveHubPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModelBase.IsBusy) || e.PropertyName == nameof(ViewModelBase.BusyText))
            {
                UpdateBusyState();
            }
        }

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
        private string _userActivityStatus = "Ready";

        [ObservableProperty]
        private string _dbStatusText = "Connected";

        [ObservableProperty]
        private bool _isDbConnected = true;

        [ObservableProperty]
        private string _onlineCount = "0";

        [ObservableProperty]
        private ObservableCollection<UserDisplayModel> _connectedUsers = new();

        [ObservableProperty]
        private string _currentTime = string.Empty;

        [ObservableProperty]
        private string _currentDate = string.Empty;

        [ObservableProperty]
        private bool _isUserListVisible;

        [ObservableProperty]
        private bool _isProfileMenuVisible;
        
        [ObservableProperty]
        private string _statusMessage = "Ready";

        [RelayCommand]
        private void ToggleUserList()
        {
            IsUserListVisible = !IsUserListVisible;
            if (IsUserListVisible) IsProfileMenuVisible = false;
        }

        [RelayCommand]
        private void ToggleProfileMenu()
        {
            IsProfileMenuVisible = !IsProfileMenuVisible;
            if (IsProfileMenuVisible) IsUserListVisible = false;
        }

        private readonly System.Windows.Threading.DispatcherTimer _clockTimer;

        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarMinimized = !IsSidebarMinimized;
        }

        [RelayCommand]
        private void ShowProfile()
        {
            CurrentProfile = _serviceProvider.GetRequiredService<ProfileViewModel>();
        }

        [RelayCommand]
        private void CloseProfile()
        {
            CurrentProfile = null;
        }

        [RelayCommand]
        private void ShowAbout()
        {
            IsAboutVisible = true;
        }

        [RelayCommand]
        private void CloseAbout()
        {
            IsAboutVisible = false;
        }

        [RelayCommand]
        private async Task Logout()
        {
            await _authService.LogoutAsync();
            Navigation.NavigateTo("Auth");
        }

        public MainViewModel(
            INavigationService navigation, 
            IPermissionService permissionService, 
            IAuthService authService, 
            ISignalRService signalRService, 
            IServiceProvider serviceProvider, 
            IFeatureService featureService,
            UserActivityService userActivityService,
            IHttpClientFactory httpClientFactory,
            ConnectionSettings connectionSettings)
        {
            _navigation = navigation;
            _permissionService = permissionService;
            _authService = authService;
            _signalRService = signalRService;
            _serviceProvider = serviceProvider;
            _featureService = featureService;
            _userActivityService = userActivityService;
            _httpClientFactory = httpClientFactory;
            _connectionSettings = connectionSettings;

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
            
            // Start minimized as requested
            IsSidebarMinimized = true;

            InitializeNavigation();
            UpdateFilteredNavigationItems();
            
            // Setup CollectionView filtering/grouping
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(NavigationItems);
            view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(NavItem.Category)));

            _clockTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => UpdateTime();
            _clockTimer.Start();
            UpdateTime(); // initial call

            // Register for messages
            WeakReferenceMessenger.Default.Register<ToastNotificationMessage>(this);
            WeakReferenceMessenger.Default.Register<CloseHubMessage>(this);
            WeakReferenceMessenger.Default.Register<OpenHubMessage>(this);
            WeakReferenceMessenger.Default.Register<OpenProjectMessage>(this);
            WeakReferenceMessenger.Default.Register<StatusUpdateMessage>(this);
            
            _signalRService.UserListUpdated += OnUserListUpdated;
            _ = _signalRService.StartAsync();

            // Setup User Activity Monitoring
            UserActivityStatus = _userActivityService.StatusText;
            _userActivityService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(UserActivityService.StatusText))
                    UserActivityStatus = _userActivityService.StatusText;
            };

            // Start DB Polling
            StartDbPolling();
            
            // Open Dashboard by default - Removed to start blank as requested
            // OpenHub<DashboardViewModel>();
        }

        private async void StartDbPolling()
        {
            await CheckDbConnection();
            
            var timer = new System.Threading.PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync())
            {
                await CheckDbConnection();
            }
        }

        private async Task CheckDbConnection()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(_connectionSettings.ApiBaseUrl);
                
                var response = await client.GetAsync("api/health/db-check");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var dbName = "Unknown";
                    
                    try 
                    {
                        var json = System.Text.Json.JsonDocument.Parse(content);
                        // Check both common casings
                        if (json.RootElement.TryGetProperty("databaseName", out var dbProp) || 
                            json.RootElement.TryGetProperty("DatabaseName", out dbProp))
                        {
                            dbName = dbProp.GetString() ?? "Unknown";
                        }
                    }
                    catch { dbName = "Parse Error"; }

                    IsDbConnected = true;
                    DbStatusText = $"Online: {dbName}";
                }
                else
                {
                    IsDbConnected = false;
                    DbStatusText = "Offline: API Error";
                }
            }
            catch
            {
                IsDbConnected = false;
                DbStatusText = "Offline: Disconnected";
            }
        }

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

        public record UserDisplayModel(string Name, string TimeOnline, System.Windows.Media.Brush StatusColor);

        private void UpdateTime()
        {
            var now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm:ss");
            CurrentDate = now.ToString("dddd, d") + GetDaySuffix(now.Day) + now.ToString(" MMMM yyyy");
        }

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

        private void InitializeNavigation()
        {
            var items = _featureService.GetNavigationItems();

            foreach (var item in items)
            {
                // Top-level permission check or just add directly if route is empty/parent
                if (string.IsNullOrEmpty(item.Route) || _permissionService.CanAccess(item.Route))
                {
                    // Filter children by permissions
                    var accessibleChildren = item.Children.Where(c => string.IsNullOrEmpty(c.Route) || _permissionService.CanAccess(c.Route)).ToList();
                    
                    item.Children.Clear();
                    foreach (var child in accessibleChildren)
                    {
                        item.Children.Add(child);
                    }

                    // Only add parent if it has children, or if it's a standalone endpoint
                    if (item.IsParent || !string.IsNullOrEmpty(item.Route))
                    {
                        NavigationItems.Add(item);
                    }
                }
            }
        }

        [RelayCommand]
        private void ShowReportBug()
        {
            var currentVM = (ViewModelBase?)ActiveHub;
            
            // Recursively find the topmost active overlay
            while (currentVM is IOverlayProvider overlayProvider && overlayProvider.ActiveOverlay != null)
            {
                currentVM = overlayProvider.ActiveOverlay;
            }

            var viewName = currentVM?.GetType().Name.Replace("ViewModel", "View") ?? "Main Shell";
            var viewModelType = Navigation.GetViewModelTypeForRoute("Support.ReportBug");
            if (viewModelType == null) return;

            var hub = (ViewModelBase)_serviceProvider.GetRequiredService(viewModelType);
            
            // Use dynamic cast to call Initialize since ReportBugViewModel might be in a different module
            (hub as dynamic).Initialize(viewName);
            CurrentReportBug = hub;
        }

        [RelayCommand]
        private void ShowSupportHub()
        {
            OpenHub("Support.SupportHub");
        }


        [RelayCommand]
        private void ExitApp()
        {
            System.Windows.Application.Current.Shutdown();
        }

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

            // If it's a parent node, just expand/collapse it
            if (item.IsParent)
            {
                item.IsExpanded = !item.IsExpanded;
                return;
            }

            if (string.IsNullOrEmpty(item.Route)) return;
            
            HandleRoute(item.Route);

            // Sync sidebar state
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

        private void HandleRoute(string route)
        {
            if (route == "Support.ReportBug")
            {
                ShowReportBug();
                return;
            }

            OpenHub(route);
        }

        [RelayCommand]
        private void CloseHub(ViewModelBase hub)
        {
            // We can't use type check for Dashboard anymore if it's not imported, 
            // but we can check the title or a property.
            if (hub.Title == "Dashboard") return;
            
            if (hub == CurrentReportBug)
            {
                CurrentReportBug = null;
                return;
            }

            OpenHubs.Remove(hub);
            if (ActiveHub == hub)
            {
                ActiveHub = OpenHubs.LastOrDefault();
            }
        }

        [RelayCommand]
        private void NavigateToHub(ViewModelBase hub)
        {
            ActiveHub = hub;
        }

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

        [RelayCommand]
        private void CloseAllTabs()
        {
            OpenHubs.Clear();
            ActiveHub = null;
        }

        [RelayCommand]
        private void CloseOtherTabs(ViewModelBase currentHub)
        {
            var hubToKeep = currentHub ?? ActiveHub;
            if (hubToKeep == null) return;
            
            var hubsToRemove = OpenHubs.Where(h => h != hubToKeep).ToList();
            foreach (var hub in hubsToRemove)
            {
                OpenHubs.Remove(hub);
            }
            ActiveHub = hubToKeep;
        }

        [RelayCommand]
        private void CloseTabsToRight(ViewModelBase currentHub)
        {
            var referenceHub = currentHub ?? ActiveHub;
            if (referenceHub == null) return;
            
            var index = OpenHubs.IndexOf(referenceHub);
            if (index >= 0)
            {
                while (OpenHubs.Count > index + 1)
                {
                    OpenHubs.RemoveAt(OpenHubs.Count - 1);
                }
            }
            ActiveHub = referenceHub;
        }

        partial void OnFeatureSearchQueryChanged(string value)
        {
            UpdateFilteredNavigationItems();
        }

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
                // Check parent
                bool parentMatches = item.Label.ToLower().Contains(query);
                
                // Check children
                var matchedChildren = item.Children
                    .Where(c => c.Label.ToLower().Contains(query))
                    .ToList();

                if (parentMatches || matchedChildren.Any())
                {
                    // Create a result item that includes the matches
                    var resultItem = new NavItem(item.Label, "IconSummary", item.Route, item.Category)
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

        public void Receive(OpenHubMessage message)
        {
            OpenHub(message.Value);
        }

        public void Receive(CloseHubMessage message)
        {
            CloseHub(message.Value);
        }

        public void Receive(OpenProjectMessage message)
        {
            var projectId = message.Value;
            
            // Check if already open
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

        public void Receive(ToastNotificationMessage message)
        {
            var toast = message.Value;
            
            // UI thread safety
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                 Toasts.Add(toast);
            });

            // Auto-remove after 5 seconds
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                
                // Fade out
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(50);
                    System.Windows.Application.Current.Dispatcher.Invoke(() => toast.Opacity -= 0.1);
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() => Toasts.Remove(toast));
            });
        }

        public void Receive(StatusUpdateMessage message)
        {
            StatusMessage = message.Value;
            
            // Revert to "Ready" after 10 seconds if it's an action message
            if (message.Value != "Ready")
            {
                Task.Run(async () =>
                {
                    await Task.Delay(10000);
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        if (StatusMessage == message.Value) StatusMessage = "Ready";
                    });
                });
            }
        }
    }
}
