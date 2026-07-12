using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.SettingsHub.ViewModels
{
    public enum AuditLogFilter
    {
        All,
        Daily,
        Weekly,
        Monthly
    }

    public partial class AuditLogViewModel : ListViewModelBase<AuditLogDisplayModel>
    {
        private readonly IAuditLogService _auditLogService;
        private readonly IUserService _userService;
        private readonly IProjectService _projectService;
        private readonly IEmployeeService _employeeService;
        private readonly IAttendanceService _attendanceService;
        private readonly ILogger<AuditLogViewModel> _logger;

        private readonly List<AuditLogDisplayModel> _allLogs = new();
        private Dictionary<string, string>? _userMap;
        private Dictionary<string, string>? _projectMap;
        private Dictionary<string, string>? _employeeMap;
        private Dictionary<string, string>? _teamMap;

        private int _skip = 0;
        private const int PageSize = 100;

        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private User? _selectedUser;

        [ObservableProperty]
        private int _selectedTimeSpanIndex = 0; // Default: All Time

        public bool IsCustomTimeSpan => SelectedTimeSpanIndex == 7;

        [ObservableProperty]
        private DateTime? _startDate;

        [ObservableProperty]
        private DateTime? _endDate;

        [ObservableProperty]
        private bool _hasMore;

        // Stats Counters
        [ObservableProperty] private int _createCount;
        [ObservableProperty] private int _updateCount;
        [ObservableProperty] private int _deleteCount;

        public override string ReportTitle => "System Audit Logs";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Timestamp", PropertyName = "Timestamp", Width = 2.5 },
            new() { Header = "User", PropertyName = "UserName", Width = 2 },
            new() { Header = "Action", PropertyName = "Action", Width = 1.5 },
            new() { Header = "Table", PropertyName = "TableName", Width = 2 },
            new() { Header = "Record", PropertyName = "EntityName", Width = 3 }
        };

        public override IRelayCommand<object>? OpenCommand => OpenDetailsCommand;
        public override IRelayCommand<object>? EditCommand => OpenDetailsCommand;

        public AuditLogViewModel(
            IAuditLogService auditLogService,
            IUserService userService,
            IProjectService projectService,
            IEmployeeService employeeService,
            IAttendanceService attendanceService,
            ILogger<AuditLogViewModel> logger,
            IPdfService pdfService) : base(pdfService)
        {
            _auditLogService = auditLogService;
            _userService = userService;
            _projectService = projectService;
            _employeeService = employeeService;
            _attendanceService = attendanceService;
            _logger = logger;
            Title = "Audit Logs";

            _ = LoadDataAsync();
        }

        public override async Task LoadDataAsync()
        {
            await LoadPageAsync(reset: true);
        }

        public async Task LoadPageAsync(bool reset)
        {
            IsBusy = true;
            BusyText = reset ? "Loading audit logs..." : "Loading more logs...";

            try
            {
                if (reset)
                {
                    _skip = 0;
                    _allLogs.Clear();
                }

                // Lazy load reference mappings
                if (_userMap == null)
                {
                    var usersTask = _userService.GetUsersAsync();
                    var projectsTask = _projectService.GetProjectsAsync();
                    var employeesTask = _employeeService.GetEmployeesAsync();
                    var teamsTask = _attendanceService.GetTeamsAsync();

                    await Task.WhenAll(usersTask, projectsTask, employeesTask, teamsTask);

                    var users = usersTask.Result ?? Enumerable.Empty<User>();
                    var projects = projectsTask.Result ?? Enumerable.Empty<Project>();
                    var employees = employeesTask.Result ?? Enumerable.Empty<EmployeeSummaryDto>();
                    var teams = teamsTask.Result ?? Enumerable.Empty<Team>();

                    _userMap = users.ToDictionary(u => u.Id.ToString().ToLower(), u => u.DisplayName ?? u.Email);
                    _projectMap = projects.ToDictionary(p => p.Id.ToString().ToLower(), p => p.Name);
                    _employeeMap = employees.ToDictionary(e => e.Id.ToString().ToLower(), e => $"{e.FirstName} {e.LastName}".Trim());
                    _teamMap = teams.ToDictionary(t => t.Id.ToString().ToLower(), t => t.Name);

                    var sortedUsers = users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
                    var list = new List<User>
                    {
                        new User { Id = Guid.Empty, FirstName = "All", LastName = "" },
                        new User { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), FirstName = "System", LastName = "" }
                    };
                    list.AddRange(sortedUsers);
                    Users = new ObservableCollection<User>(list);
                    SelectedUser = list[0];
                }

                Guid? selectedUserId = SelectedUser?.Id;
                var resultDto = await _auditLogService.GetAuditLogsAsync(
                    SearchQuery,
                    selectedUserId,
                    StartDate,
                    EndDate,
                    _skip,
                    PageSize
                );

                if (resultDto != null)
                {
                    var userMap = _userMap!;
                    var projectMap = _projectMap!;
                    var employeeMap = _employeeMap!;
                    var teamMap = _teamMap!;

                    var mapped = resultDto.Items.Select(l =>
                    {
                        var userIdClean = (l.UserId ?? string.Empty).Trim().ToLower();
                        var userName = userMap.TryGetValue(userIdClean, out var name) ? name : l.UserId;

                        var recordIdClean = (l.RecordId ?? string.Empty).Trim();
                        if (recordIdClean.StartsWith("{") && recordIdClean.Contains("\"Id\":"))
                        {
                            try
                            {
                                int start = recordIdClean.IndexOf("\"Id\":\"") + 6;
                                if (start > 6)
                                {
                                    int end = recordIdClean.IndexOf("\"", start);
                                    if (end > start)
                                    {
                                        recordIdClean = recordIdClean.Substring(start, end - start);
                                    }
                                }
                            }
                            catch { }
                        }
                        recordIdClean = recordIdClean.ToLower();

                        string entityName = l.RecordId ?? string.Empty;
                        var tableClean = (l.TableName ?? string.Empty).Trim().ToLower();

                        if (tableClean == "project" || tableClean == "projects")
                        {
                            if (projectMap.TryGetValue(recordIdClean, out var projName)) entityName = projName;
                        }
                        else if (tableClean == "employee" || tableClean == "employees")
                        {
                            if (employeeMap.TryGetValue(recordIdClean, out var empName)) entityName = empName;
                        }
                        else if (tableClean == "team" || tableClean == "teams")
                        {
                            if (teamMap.TryGetValue(recordIdClean, out var teamName)) entityName = teamName;
                        }
                        else if (tableClean == "user" || tableClean == "users")
                        {
                            if (userMap.TryGetValue(recordIdClean, out var usrName)) entityName = usrName;
                        }

                        return new AuditLogDisplayModel(l, userName ?? "System", entityName);
                    }).ToList();

                    foreach (var item in mapped)
                    {
                        _allLogs.Add(item);
                    }

                    Items = new ObservableCollection<AuditLogDisplayModel>(_allLogs);

                    TotalCount = resultDto.TotalCount;
                    CreateCount = resultDto.CreateCount;
                    UpdateCount = resultDto.UpdateCount;
                    DeleteCount = resultDto.DeleteCount;

                    HasMore = Items.Count < TotalCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading audit logs page");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task LoadMoreAsync()
        {
            if (!HasMore || IsBusy) return;
            _skip += PageSize;
            await LoadPageAsync(reset: false);
        }

        [RelayCommand]
        private void OpenDetails(object? parameter)
        {
            var target = parameter as AuditLogDisplayModel ?? SelectedItem;
            if (target == null) return;

            var detailVm = new AuditLogDetailViewModel(target);
            OpenOverlay(detailVm);
        }

        private bool _isUpdatingTimeSpan;

        partial void OnSelectedTimeSpanIndexChanged(int value)
        {
            if (value == 7)
            {
                OnPropertyChanged(nameof(IsCustomTimeSpan));
                return;
            }

            _isUpdatingTimeSpan = true;
            try
            {
                if (value == 0) // All Time
                {
                    StartDate = null;
                    EndDate = null;
                }
                else if (value == 1) // Today
                {
                    StartDate = DateTime.Today;
                    EndDate = DateTime.Today;
                }
                else if (value == 2) // Yesterday
                {
                    StartDate = DateTime.Today.AddDays(-1);
                    EndDate = DateTime.Today.AddDays(-1);
                }
                else if (value == 3) // This Week
                {
                    DateTime start = DateTime.Today;
                    while (start.DayOfWeek != DayOfWeek.Saturday) start = start.AddDays(-1);
                    StartDate = start;
                    EndDate = start.AddDays(6);
                }
                else if (value == 4) // Last Week
                {
                    DateTime start = DateTime.Today;
                    while (start.DayOfWeek != DayOfWeek.Saturday) start = start.AddDays(-1);
                    start = start.AddDays(-7);
                    StartDate = start;
                    EndDate = start.AddDays(6);
                }
                else if (value == 5) // This Month
                {
                    DateTime firstDay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    StartDate = firstDay;
                    EndDate = firstDay.AddMonths(1).AddDays(-1);
                }
                else if (value == 6) // Last Month
                {
                    DateTime firstDay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
                    StartDate = firstDay;
                    EndDate = firstDay.AddMonths(1).AddDays(-1);
                }
            }
            finally
            {
                _isUpdatingTimeSpan = false;
            }

            OnPropertyChanged(nameof(IsCustomTimeSpan));
            FilterItems();
        }

        partial void OnSelectedUserChanged(User? value)
        {
            if (_userMap != null) // Only reload if initial load is done
            {
                FilterItems();
            }
        }

        partial void OnStartDateChanged(DateTime? value)
        {
            if (!_isUpdatingTimeSpan && SelectedTimeSpanIndex == 7 && _userMap != null)
            {
                FilterItems();
            }
        }

        partial void OnEndDateChanged(DateTime? value)
        {
            if (!_isUpdatingTimeSpan && SelectedTimeSpanIndex == 7 && _userMap != null)
            {
                FilterItems();
            }
        }

        protected override void FilterItems()
        {
            _ = LoadPageAsync(reset: true);
        }
    }
}
