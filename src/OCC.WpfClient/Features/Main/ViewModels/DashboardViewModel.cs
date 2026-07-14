using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.Main.Models;
using OCC.WpfClient.Features.CalendarHub.Services;
using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        #region Private Fields

        private static readonly System.Collections.Generic.HashSet<Guid> _shownBirthdayEmployeeIds = new();
        private static readonly System.Collections.Generic.HashSet<Guid> _shownPassportEmployeeIds = new();
        private static DateTime _lastBirthdayCheckDate;

        private readonly IUserService _userService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        private readonly IProjectTaskService _taskService;
        private readonly IEmployeeService _employeeService;
        private readonly ICalendarService _calendarService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConnectionSettings _connectionSettings;
        private readonly ILocalEncryptionService _encryptionService;
        private readonly ISignalRService _signalRService;
        private readonly IPermissionService _permissionService;
        private readonly LocalSettingsService _localSettingsService;
        private readonly IBugReportService _bugService;
        private readonly ITodoService _todoService;
        private readonly INoticeBoardService _noticeBoardService;

        [ObservableProperty]
        private bool _isLoading;

        private List<WidgetViewModelBase> _allPossibleWidgets = new();

        #endregion

        #region Properties & Observables

        [ObservableProperty]
        private string _userName = "User";

        [ObservableProperty]
        private string _currentDate = DateTime.Now.ToString("dd MMMM yyyy");

        [ObservableProperty]
        private string _greeting = "Good day";

        [ObservableProperty]
        private ObservableCollection<WidgetViewModelBase> _activeWidgets = new();

        public double GridHeight
        {
            get
            {
                if (IsEditMode) return 2200;
                
                int maxRow = ActiveWidgets.Any() ? ActiveWidgets.Max(w => w.Row + w.RowSpan) : 0;
                return maxRow * 110 + 20;
            }
        }

        [ObservableProperty]
        private ObservableCollection<WidgetViewModelBase> _availableWidgets = new();

        [ObservableProperty]
        private bool _isEditMode;

        #endregion

        #region Constructors

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
            IBugReportService bugService,
            ITodoService todoService,
            INoticeBoardService noticeBoardService)
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
            _todoService = todoService;
            _noticeBoardService = noticeBoardService;
            Title = "Dashboard";
            
            UserName = _authService.CurrentUser?.DisplayName ?? "User";
            Greeting = GetGreeting();
            
            _signalRService.ChatMessageReceived += OnChatMessageReceived;

            InitializeWidgets();
            _ = LoadData();
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadData();
        }

        [RelayCommand]
        private void CustomizeLayout()
        {
            IsEditMode = !IsEditMode;
            foreach (var widget in _allPossibleWidgets)
            {
                widget.IsEditMode = IsEditMode;
            }
            LoadWidgetLayout();
            OnPropertyChanged(nameof(GridHeight));
        }

        [RelayCommand]
        private void AddWidget(WidgetViewModelBase widget)
        {
            widget.IsVisible = true;
            
            // Put it at the end of the grid
            int maxRow = ActiveWidgets.Count > 0 ? ActiveWidgets.Max(w => w.Row) : 0;
            widget.Row = maxRow + 1;
            widget.Column = 0;
            widget.ColumnSpan = 1;

            SaveWidgetLayout();
            LoadWidgetLayout();
        }

        [RelayCommand]
        private void ResetLayout()
        {
            _localSettingsService.Settings.DashboardWidgets = null;
            _localSettingsService.Save();
            LoadWidgetLayout();
        }

        #endregion

        #region Methods

        private void InitializeWidgets()
        {
            _allPossibleWidgets = new List<WidgetViewModelBase>
            {
                new BirthdayWidgetViewModel(_employeeService, _authService),
                new AlertsWidgetViewModel(_employeeService),
                new TasksWidgetViewModel(_taskService),
                new TodosWidgetViewModel(_todoService),
                new UsersWidgetViewModel(_userService),
                new ProductivityWidgetViewModel(_taskService),
                new CalendarWidgetViewModel(_calendarService),
                new ChatsWidgetViewModel(_httpClientFactory, _connectionSettings, _authService, _encryptionService),
                new SupportWidgetViewModel(_bugService, _authService, _permissionService),
                new QuickActionsWidgetViewModel(_permissionService, _localSettingsService),
                new NoticeBoardWidgetViewModel(_noticeBoardService, _authService, _toastService)
            };

            foreach (var widget in _allPossibleWidgets)
            {
                widget.LayoutChanged += OnWidgetLayoutChanged;
            }
        }

        private void OnWidgetLayoutChanged(object? sender, string widgetId)
        {
            SaveWidgetLayout();
            LoadWidgetLayout();
        }

        public void ResolveOverlaps(WidgetViewModelBase draggedWidget)
        {
            bool changed;
            do
            {
                changed = false;
                var activeList = ActiveWidgets.ToList();
                
                foreach (var w in activeList)
                {
                    if (w == draggedWidget) continue;
                    
                    if (Intersects(w, draggedWidget))
                    {
                        w.Row = draggedWidget.Row + draggedWidget.RowSpan;
                        changed = true;
                    }
                }
                
                for (int i = 0; i < activeList.Count; i++)
                {
                    for (int j = 0; j < activeList.Count; j++)
                    {
                        if (i == j) continue;
                        var w1 = activeList[i];
                        var w2 = activeList[j];
                        
                        if (Intersects(w1, w2))
                        {
                            if (w1.Row < w2.Row)
                            {
                                w2.Row = w1.Row + w1.RowSpan;
                            }
                            else
                            {
                                w1.Row = w2.Row + w2.RowSpan;
                            }
                            changed = true;
                        }
                    }
                }
            } while (changed);
            
            SaveWidgetLayout();
            LoadWidgetLayout();
        }

        private bool Intersects(WidgetViewModelBase w1, WidgetViewModelBase w2)
        {
            if (!w1.IsVisible || !w2.IsVisible) return false;
            
            bool xOverlap = w1.Column < w2.Column + w2.ColumnSpan && w1.Column + w1.ColumnSpan > w2.Column;
            bool yOverlap = w1.Row < w2.Row + w2.RowSpan && w1.Row + w1.RowSpan > w2.Row;
            
            return xOverlap && yOverlap;
        }

        private void LoadWidgetLayout()
        {
            var saved = _localSettingsService.Settings.DashboardWidgets;
            if (saved == null || saved.Count == 0)
            {
                saved = new List<WidgetConfig>
                {
                    new WidgetConfig { WidgetId = "Birthday", Row = 0, Column = 0, ColumnSpan = 4, IsVisible = false },
                    new WidgetConfig { WidgetId = "Alerts", Row = 1, Column = 0, ColumnSpan = 4, IsVisible = true },
                    new WidgetConfig { WidgetId = "Tasks", Row = 2, Column = 0, ColumnSpan = 1, IsVisible = true },
                    new WidgetConfig { WidgetId = "Todos", Row = 2, Column = 1, ColumnSpan = 1, IsVisible = true },
                    new WidgetConfig { WidgetId = "Users", Row = 2, Column = 2, ColumnSpan = 1, IsVisible = true },
                    new WidgetConfig { WidgetId = "Productivity", Row = 2, Column = 3, ColumnSpan = 1, IsVisible = true },
                    new WidgetConfig { WidgetId = "Calendar", Row = 3, Column = 0, ColumnSpan = 2, IsVisible = true },
                    new WidgetConfig { WidgetId = "Chats", Row = 3, Column = 2, ColumnSpan = 1, IsVisible = true },
                    new WidgetConfig { WidgetId = "Support", Row = 4, Column = 0, ColumnSpan = 2, IsVisible = true },
                    new WidgetConfig { WidgetId = "QuickActions", Row = 5, Column = 0, ColumnSpan = 4, IsVisible = true }
                };
                _localSettingsService.Settings.DashboardWidgets = saved;
                _localSettingsService.Save();
            }

            var activeList = new List<WidgetViewModelBase>();
            var availableList = new List<WidgetViewModelBase>();

            foreach (var cfg in saved)
            {
                var widget = _allPossibleWidgets.FirstOrDefault(w => w.WidgetId == cfg.WidgetId);
                if (widget != null)
                {
                    widget.Row = cfg.Row;
                    widget.Column = cfg.Column;
                    widget.ColumnSpan = cfg.ColumnSpan;
                    widget.RowSpan = cfg.RowSpan;
                    widget.IsVisible = cfg.IsVisible;
                    widget.IsEditMode = IsEditMode;

                    // If it is the birthday banner, it's only active/visible when there's an actual birthday message
                    if (cfg.WidgetId == "Birthday" && widget is BirthdayWidgetViewModel bVm && string.IsNullOrEmpty(bVm.BirthdayGreeting))
                    {
                        // Keep it hidden/out of layout
                        continue;
                    }

                    if (widget.IsVisible)
                    {
                        activeList.Add(widget);
                    }
                    else
                    {
                        availableList.Add(widget);
                    }
                }
            }

            ActiveWidgets = new ObservableCollection<WidgetViewModelBase>(activeList.OrderBy(w => w.Row).ThenBy(w => w.Column));
            AvailableWidgets = new ObservableCollection<WidgetViewModelBase>(availableList);
            OnPropertyChanged(nameof(GridHeight));
        }

        private void SaveWidgetLayout()
        {
            var saved = new List<WidgetConfig>();
            foreach (var widget in _allPossibleWidgets)
            {
                saved.Add(new WidgetConfig
                {
                    WidgetId = widget.WidgetId,
                    Row = widget.Row,
                    Column = widget.Column,
                    ColumnSpan = widget.ColumnSpan,
                    RowSpan = widget.RowSpan,
                    IsVisible = widget.IsVisible
                });
            }
            _localSettingsService.Settings.DashboardWidgets = saved;
            _localSettingsService.Save();
        }

        private async Task LoadData()
        {
            if (IsLoading) return;
            try
            {
                IsLoading = true;
                var startTime = DateTime.UtcNow;

                UserName = _authService.CurrentUser?.DisplayName ?? "User";
                Greeting = GetGreeting();

                var today = DateTime.Today;

                // Daily check resetting for notifications
                if (_lastBirthdayCheckDate != today)
                {
                    _shownBirthdayEmployeeIds.Clear();
                    _shownPassportEmployeeIds.Clear();
                    _lastBirthdayCheckDate = today;
                }

                // Run notifications check once in background
                try
                {
                    var employees = await _employeeService.GetEmployeesAsync();
                    var birthdayEmployees = employees
                        .Where(e => e.Status == EmployeeStatus.Active &&
                                    e.DoB.Month == today.Month &&
                                    e.DoB.Day == today.Day)
                        .ToList();

                    foreach (var emp in birthdayEmployees)
                    {
                        if (!_shownBirthdayEmployeeIds.Contains(emp.Id))
                        {
                            _toastService.ShowInfo("Employee Birthday 🎉", $"Happy Birthday to {emp.DisplayName}! 🎂");
                            _shownBirthdayEmployeeIds.Add(emp.Id);
                        }
                    }

                    var expiringPassports = employees
                        .Where(e => e.Status == EmployeeStatus.Active && e.IdType == IdType.Passport &&
                                    (!e.PassportStampDate.HasValue || (e.PassportStampDate.Value.Date.AddDays(90) - today).TotalDays <= 60))
                        .ToList();

                    foreach (var emp in expiringPassports)
                    {
                        if (!_shownPassportEmployeeIds.Contains(emp.Id))
                        {
                            var remainingDays = emp.PassportStampDate.HasValue 
                                ? (int)(90 - (today - emp.PassportStampDate.Value.Date).TotalDays)
                                : 0;

                            string message;
                            if (!emp.PassportStampDate.HasValue)
                            {
                                message = $"{emp.DisplayName} has no passport stamp date set!";
                            }
                            else if (remainingDays < 0)
                            {
                                message = $"{emp.DisplayName}'s passport stamp expired {-remainingDays} days ago (stamped on {emp.PassportStampDate.Value:yyyy-MM-dd}).";
                            }
                            else if (remainingDays == 0)
                            {
                                message = $"{emp.DisplayName}'s passport stamp expires today (stamped on {emp.PassportStampDate.Value:yyyy-MM-dd}).";
                            }
                            else
                            {
                                message = $"{emp.DisplayName}'s passport stamp expires in {remainingDays} days (stamped on {emp.PassportStampDate.Value:yyyy-MM-dd}).";
                            }

                            _toastService.ShowWarning("Passport Stamp Expiry Warning", message);
                            _shownPassportEmployeeIds.Add(emp.Id);
                        }
                    }
                }
                catch { }

                // Identify which widgets are configured to be visible
                var saved = _localSettingsService.Settings.DashboardWidgets;
                if (saved == null || saved.Count == 0)
                {
                    LoadWidgetLayout();
                    saved = _localSettingsService.Settings.DashboardWidgets;
                }

                var activeIds = saved?.Where(cfg => cfg.IsVisible).Select(cfg => cfg.WidgetId).ToList() ?? new List<string>();

                // Birthday widget layout visibility dynamically checks for greetings,
                // so we must always refresh it if it's configured visible.
                if (saved != null && saved.Any(cfg => cfg.WidgetId == "Birthday" && cfg.IsVisible))
                {
                    if (!activeIds.Contains("Birthday"))
                    {
                        activeIds.Add("Birthday");
                    }
                }

                // Refresh only active widgets in parallel to save API requests and bandwidth
                var activeWidgetsToRefresh = _allPossibleWidgets.Where(w => activeIds.Contains(w.WidgetId));
                var refreshTasks = activeWidgetsToRefresh.Select(w => w.RefreshDataAsync());
                await Task.WhenAll(refreshTasks);

                LoadWidgetLayout();

                // Enforce a minimum display time of 4 seconds for the loading animation
                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed.TotalMilliseconds < 4000)
                {
                    var remaining = 4000 - (int)elapsed.TotalMilliseconds;
                    await Task.Delay(remaining);
                }
            }
            catch 
            { 
                // Ignore silent load failures
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string GetGreeting()
        {
            var hour = DateTime.Now.Hour;
            if (hour < 12) return "Good morning";
            if (hour < 18) return "Good afternoon";
            return "Good evening";
        }

        private void OnChatMessageReceived(ChatMessageDto message)
        {
            // Refresh chats widget
            var chatWidget = _allPossibleWidgets.FirstOrDefault(w => w.WidgetId == "Chats");
            if (chatWidget != null)
            {
                _ = chatWidget.RefreshDataAsync();
            }
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
}
