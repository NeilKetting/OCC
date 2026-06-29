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

        [ObservableProperty]
        private bool _isLoadingEvents;

        [ObservableProperty]
        private bool _isLoadingChats;

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
            ConnectionSettings connectionSettings)
        {
            _userService = userService;
            _toastService = toastService;
            _authService = authService;
            _taskService = taskService;
            _employeeService = employeeService;
            _calendarService = calendarService;
            _httpClientFactory = httpClientFactory;
            _connectionSettings = connectionSettings;
            Title = "Dashboard";
            
            UserName = _authService.CurrentUser?.DisplayName ?? "User";
            Greeting = GetGreeting();
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

        #endregion

        #region Methods

        /// <summary>
        /// Loads user count and user task statistics from services.
        /// </summary>
        private async System.Threading.Tasks.Task LoadData()
        {
            try
            {
                var users = await _userService.GetUsersAsync();
                UserCount = users.Count();

                var tasks = await _taskService.GetTasksAsync(assignedToMe: true);
                var taskList = tasks.ToList();
                
                TaskCount = taskList.Count(t => !t.IsComplete);
                TodoCount = taskList.Count(t => t.Type == TaskType.PersonalToDo && !t.IsComplete);

                _toastService.ShowSuccess("System Active", "Toast Notification System is now live!");

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
            }
            catch 
            { 
                // Fallback / Ignore background load failures silently
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

        #endregion
    }
}
