using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.ObjectModel;
using OCC.WpfClient.Features.CalendarHub.Services;
using OCC.WpfClient.Features.CalendarHub.Models;
using OCC.Shared.DTOs;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    /// <summary>
    /// ViewModel for the main dashboard view, displaying metrics like active task counts,
    /// todo counts, user counts, and general welcome greetings.
    /// </summary>
    public partial class DashboardViewModel : ViewModelBase
    {
        #region Private Fields

        // Manages user querying
        private readonly IUserService _userService;

        // Handles system toast notifications
        private readonly IToastService _toastService;

        // Provides current authenticated user details
        private readonly IAuthService _authService;

        // Retrieves project and personal tasks
        private readonly IProjectTaskService _taskService;

        // Manages employee querying (specifically birthdays)
        private readonly IEmployeeService _employeeService;

        // Injected calendar aggregator
        private readonly ICalendarService _calendarService;

        // Network HTTP calls for unread chats
        private readonly IHttpClientFactory _httpClientFactory;

        // Environment URLs
        private readonly ConnectionSettings _connectionSettings;

        // Decryption of chat messages
        private readonly ILocalEncryptionService _encryptionService;

        // SignalR for real-time unread messages
        private readonly ISignalRService _signalRService;

        // Permissions check for quick actions & navigation
        private readonly IPermissionService _permissionService;

        // Access to user preferences for quick actions
        private readonly LocalSettingsService _localSettingsService;

        // Retrieves support tickets
        private readonly IBugReportService _bugService;

        private bool _isLoadingData;

        [ObservableProperty]
        private bool _isLoadingSupportTickets;

        #endregion

        #region Properties & Observables

        // Display name of the logged-in user
        [ObservableProperty]
        private string _userName = "User";

        // Count of active/uncompleted tasks assigned to the user
        [ObservableProperty]
        private int _taskCount;

        // Count of personal todo items remaining for the user
        [ObservableProperty]
        private int _todoCount;

        // Total count of registered users in the system
        [ObservableProperty]
        private int _userCount;

        // The current calendar date formatted for display
        [ObservableProperty]
        private string _currentDate = DateTime.Now.ToString("dd MMMM yyyy");

        // The welcome greeting adjusted to the time of day
        [ObservableProperty]
        private string _greeting = "Good day";

        // Birthday Greeting banner message (empty means hidden)
        [ObservableProperty]
        private string _birthdayGreeting = string.Empty;

        // Collections for upcoming events and unread chats
        public ObservableCollection<CalendarEvent> UpcomingEvents { get; } = new();
        public ObservableCollection<ChatSessionDto> UnreadSessions { get; } = new();
        public ObservableCollection<BugReport> OpenSupportTickets { get; } = new();
        public ObservableCollection<BugReport> SupportTicketsNeedingFeedback { get; } = new();

        [ObservableProperty]
        private bool _isLoadingEvents;

        [ObservableProperty]
        private bool _isLoadingChats;

        // The task completion rate percentage
        [ObservableProperty]
        private int _completionRate;

        // Subtext description of completed tasks (e.g. "x of y completed")
        [ObservableProperty]
        private string _completionRateText = string.Empty;

        [ObservableProperty]
        private bool _isShortcutPickerOpen;

        public ObservableCollection<QuickShortcutOption> ActiveQuickActions { get; } = new();
        public ObservableCollection<QuickShortcutOption> ShortcutOptions { get; } = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the dashboard, setting the username, determining the time-of-day greeting,
        /// and triggering background data loading.
        /// </summary>
        public DashboardViewModel(
            IUserService userService, 
            IToastService toastService, 
            IAuthService authService,
            IProjectTaskService taskService,
            IEmployeeService employeeService,
            ICalendarService calendarService,
            IHttpClientFactory httpClientFactory,
            ConnectionSettings connectionSettings,
            ILocalEncryptionService encryptionService,
            ISignalRService signalRService,
            IPermissionService permissionService,
            LocalSettingsService localSettingsService,
            IBugReportService bugService)
        {
            _userService = userService;
            _toastService = toastService;
            _authService = authService;
            _taskService = taskService;
            _employeeService = employeeService;
            _calendarService = calendarService;
            _httpClientFactory = httpClientFactory;
            _connectionSettings = connectionSettings;
            _encryptionService = encryptionService;
            _signalRService = signalRService;
            _permissionService = permissionService;
            _localSettingsService = localSettingsService;
            _bugService = bugService;
            Title = "Dashboard";
            
            UserName = _authService.CurrentUser?.DisplayName ?? "User";
            Greeting = GetGreeting();
            InitializeQuickActions();
            
            _signalRService.ChatMessageReceived += OnChatMessageReceived;
            
            _ = LoadData();
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void NavigateToChatSession(ChatSessionDto? session)
        {
            if (session == null) return;
            WeakReferenceMessenger.Default.Send(new OpenChatSessionMessage(session.Id));
        }

        [RelayCommand]
        private void NavigateToRoute(string route)
        {
            if (string.IsNullOrEmpty(route)) return;
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(route));
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task Refresh()
        {
            await LoadData();
        }

        [RelayCommand]
        private void NavigateToEvent(CalendarEvent ev)
        {
            if (ev == null) return;
            if (ev.Type == CalendarEventType.Task && ev.OriginalSource is ProjectTask task)
            {
                if (task.ProjectId.HasValue)
                {
                    WeakReferenceMessenger.Default.Send(new OpenProjectTaskMessage(task.ProjectId.Value, task.Id));
                }
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new OpenHubMessage("Calendar"));
            }
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

        #endregion

        #region Methods

        /// <summary>
        /// Loads user count and user task statistics from services.
        /// </summary>
        private async System.Threading.Tasks.Task LoadData()
        {
            if (_isLoadingData) return;
            try
            {
                _isLoadingData = true;

                var users = await _userService.GetUsersAsync();
                UserCount = users.Count();

                var tasks = await _taskService.GetTasksAsync(assignedToMe: true);
                var taskList = tasks.ToList();
                
                TaskCount = taskList.Count(t => !t.IsComplete);
                TodoCount = taskList.Count(t => t.Type == TaskType.PersonalToDo && !t.IsComplete);

                // Calculate real productivity/completion stats dynamically
                var totalTasksCount = taskList.Count;
                var completedTasksCount = taskList.Count(t => t.IsComplete);
                CompletionRate = totalTasksCount > 0 ? (int)Math.Round((double)completedTasksCount / totalTasksCount * 100) : 100;
                CompletionRateText = $"{completedTasksCount} of {totalTasksCount} tasks done";

                // Check for employee birthdays today and trigger sticky toasts / local greetings
                try
                {
                    var today = DateTime.Today;
                    var employees = await _employeeService.GetEmployeesAsync();
                    var birthdayEmployees = employees
                        .Where(e => e.Status == EmployeeStatus.Active &&
                                    e.DoB.Month == today.Month &&
                                    e.DoB.Day == today.Day)
                        .ToList();

                    foreach (var emp in birthdayEmployees)
                    {
                        _toastService.ShowInfo("Employee Birthday 🎉", $"Happy Birthday to {emp.DisplayName}! 🎂", isSticky: true);
                    }

                    // Check if today is the CURRENT LOGGED-IN user's birthday!
                    var currentUserId = _authService.CurrentUser?.Id;
                    var selfEmployee = employees.FirstOrDefault(e => e.Status == EmployeeStatus.Active && e.LinkedUserId == currentUserId);
                    if (selfEmployee != null && selfEmployee.DoB.Month == today.Month && selfEmployee.DoB.Day == today.Day)
                    {
                        int age = today.Year - selfEmployee.DoB.Year;
                        if (selfEmployee.DoB.Date > today.AddYears(-age)) age--;

                        var random = new Random();
                        var selfMessages = new[]
                        {
                            $"Happy {age}th Birthday! We wish you a fantastic day ahead filled with joy and success! 🎉🎂",
                            $"Congratulations on turning {age}! Have a wonderful birthday! 🥳🧁",
                            $"Happy Birthday! Cheers to {age} years of greatness! 🥂🎉",
                            $"Happy {age}th Birthday from everyone here at OCC! Hope your day is amazing! 🎈🎁"
                        };
                        BirthdayGreeting = selfMessages[random.Next(selfMessages.Length)];
                    }
                    else
                    {
                        BirthdayGreeting = string.Empty;
                    }
                }
                catch
                {
                    // Non-critical background failure
                }

                // Load upcoming calendar events
                _ = LoadUpcomingEventsAsync();

                // Load unread chats
                _ = LoadUnreadChatsAsync();

                // Load support tickets
                _ = LoadSupportTicketsAsync();
            }
            catch 
            { 
                // Fallback / Ignore background load failures silently
            }
            finally
            {
                _isLoadingData = false;
            }
        }

        public async System.Threading.Tasks.Task LoadUpcomingEventsAsync()
        {
            try
            {
                IsLoadingEvents = true;
                var today = DateTime.Today;
                var endOfNextWeek = today.AddDays(7);
                var events = await _calendarService.GetEventsAsync(today, endOfNextWeek);
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    UpcomingEvents.Clear();
                    // Sort events by StartDate, then Title
                    var sortedEvents = events.OrderBy(e => e.StartDate).ThenBy(e => e.Title).ToList();
                    foreach (var ev in sortedEvents)
                    {
                        UpcomingEvents.Add(ev);
                    }
                });
            }
            catch
            {
                // Ignore background errors
            }
            finally
            {
                IsLoadingEvents = false;
            }
        }

        public async System.Threading.Tasks.Task LoadUnreadChatsAsync()
        {
            if (_authService.CurrentToken == null) return;
            try
            {
                IsLoadingChats = true;
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.CurrentToken);
                
                var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                var sessions = await client.GetFromJsonAsync<ChatSessionDto[]>($"{baseUrl}/api/messages/sessions");
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    UnreadSessions.Clear();
                    if (sessions != null)
                    {
                        var unreadList = sessions.Where(s => s.UnreadCount > 0).OrderByDescending(s => s.LastMessage?.SentDate ?? s.CreatedAtUtc).ToList();
                        foreach (var s in unreadList)
                        {
                            // If direct chat, decrypt/ensure last message or display Name properly
                            if (!s.IsGroupChat && string.IsNullOrEmpty(s.Name))
                            {
                                var otherUser = s.Users.FirstOrDefault(u => u.UserId != _authService.CurrentUser?.Id);
                                if (otherUser != null)
                                {
                                    s.Name = $"{otherUser.FirstName} {otherUser.LastName}";
                                }
                            }
                            if (s.LastMessage != null && !s.LastMessage.HasAttachment && !string.IsNullOrEmpty(s.SharedAesKey))
                            {
                                s.LastMessage.Content = _encryptionService.DecryptMessage(s.LastMessage.Content, s.SharedAesKey);
                            }
                            UnreadSessions.Add(s);
                        }
                    }
                });
            }
            catch
            {
                // Ignore background errors
            }
            finally
            {
                IsLoadingChats = false;
            }
        }

        public async System.Threading.Tasks.Task LoadSupportTicketsAsync()
        {
            try
            {
                IsLoadingSupportTickets = true;

                var bugs = await _bugService.GetBugReportsAsync(includeArchived: false);
                var bugList = bugs.ToList();

                var currentUserId = _authService.CurrentUser?.Id;
                var isDevOrAdmin = _permissionService.IsDev || _authService.CurrentUser?.UserRole == UserRole.Admin;

                var openBugs = new List<BugReport>();
                var needingFeedback = new List<BugReport>();

                if (isDevOrAdmin)
                {
                    // Admin / Dev sees all open support tickets
                    openBugs = bugList.Where(b => b.Status != "Closed" && b.Status != "Resolved").ToList();
                }
                else
                {
                    // Regular user sees their own open tickets
                    var myBugs = bugList.Where(b => b.ReporterId == currentUserId && b.Status != "Closed" && b.Status != "Resolved").ToList();
                    openBugs = myBugs;

                    // Fetch full details of user's active tickets to check for developer comments or "Waiting for Client" status
                    foreach (var bugSummary in myBugs)
                    {
                        var fullBug = await _bugService.GetBugReportAsync(bugSummary.Id);
                        if (fullBug != null)
                        {
                            var lastComment = fullBug.Comments.OrderBy(c => c.CreatedAtUtc).LastOrDefault();
                            bool lastCommentIsFromDev = lastComment != null && (lastComment.IsDevComment || lastComment.AuthorEmail != _authService.CurrentUser?.Email);

                            if (fullBug.Status == "Waiting for Client" || lastCommentIsFromDev)
                            {
                                needingFeedback.Add(fullBug);
                            }
                        }
                    }
                }

                App.Current.Dispatcher.Invoke(() =>
                {
                    OpenSupportTickets.Clear();
                    SupportTicketsNeedingFeedback.Clear();

                    foreach (var bug in openBugs.Take(10))
                    {
                        OpenSupportTickets.Add(bug);
                    }
                    foreach (var bug in needingFeedback.Take(10))
                    {
                        SupportTicketsNeedingFeedback.Add(bug);
                    }
                });
            }
            catch
            {
                // Ignore background errors
            }
            finally
            {
                IsLoadingSupportTickets = false;
            }
        }

        /// <summary>
        /// Dynamically calculates the greeting based on the current hour of the day.
        /// </summary>
        private string GetGreeting()
        {
            var hour = DateTime.Now.Hour;
            if (hour < 12) return "Good morning";
            if (hour < 18) return "Good afternoon";
            return "Good evening";
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

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(IsActiveHub) && IsActiveHub)
            {
                _ = LoadData();
            }
        }

        private void OnChatMessageReceived(ChatMessageDto message)
        {
            _ = LoadUnreadChatsAsync();
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_signalRService != null)
            {
                _signalRService.ChatMessageReceived -= OnChatMessageReceived;
            }
        }

        #endregion
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
