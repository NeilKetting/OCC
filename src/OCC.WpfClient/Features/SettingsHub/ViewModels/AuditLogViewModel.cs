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

        private List<AuditLogDisplayModel> _allLogs = new();

        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private User? _selectedUser;

        [ObservableProperty]
        private AuditLogFilter _currentFilter = AuditLogFilter.All;

        [ObservableProperty]
        private DateTime? _startDate;

        [ObservableProperty]
        private DateTime? _endDate;

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
            try
            {
                IsBusy = true;
                BusyText = "Loading audit logs...";

                var logsTask = _auditLogService.GetAuditLogsAsync();
                var usersTask = _userService.GetUsersAsync();
                var projectsTask = _projectService.GetProjectsAsync();
                var employeesTask = _employeeService.GetEmployeesAsync();
                var teamsTask = _attendanceService.GetTeamsAsync();

                await Task.WhenAll(logsTask, usersTask, projectsTask, employeesTask, teamsTask);

                var logs = logsTask.Result ?? Enumerable.Empty<AuditLog>();
                var users = usersTask.Result ?? Enumerable.Empty<User>();
                var projects = projectsTask.Result ?? Enumerable.Empty<Project>();
                var employees = employeesTask.Result ?? Enumerable.Empty<EmployeeSummaryDto>();
                var teams = teamsTask.Result ?? Enumerable.Empty<Team>();

                // Build mapping dictionaries to resolve GUID strings into display names
                var userMap = users.ToDictionary(u => u.Id.ToString().ToLower(), u => u.DisplayName ?? u.Email);
                var projectMap = projects.ToDictionary(p => p.Id.ToString().ToLower(), p => p.Name);
                var employeeMap = employees.ToDictionary(e => e.Id.ToString().ToLower(), e => $"{e.FirstName} {e.LastName}".Trim());
                var teamMap = teams.ToDictionary(t => t.Id.ToString().ToLower(), t => t.Name);

                Users = new ObservableCollection<User>(users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName));

                _allLogs = logs.Select(l =>
                {
                    // Map operator user name
                    var userIdClean = (l.UserId ?? string.Empty).Trim().ToLower();
                    var userName = userMap.TryGetValue(userIdClean, out var name) ? name : l.UserId;

                    // Clean target RecordId if it is serialised JSON
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

                    // Resolve display name for the entity
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
                }).OrderByDescending(l => l.Timestamp).ToList();

                FilterItems();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading audit log records");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void OpenDetails(object? parameter)
        {
            var target = parameter as AuditLogDisplayModel ?? SelectedItem;
            if (target == null) return;

            var detailVm = new AuditLogDetailViewModel(target);
            OpenOverlay(detailVm);
        }

        [RelayCommand]
        private void SetPresetFilter(AuditLogFilter filter)
        {
            CurrentFilter = filter;
            // Clear custom dates if using a preset
            StartDate = null;
            EndDate = null;
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));
            FilterItems();
        }

        partial void OnSelectedUserChanged(User? value) => FilterItems();
        partial void OnStartDateChanged(DateTime? value) => FilterItems();
        partial void OnEndDateChanged(DateTime? value) => FilterItems();

        protected override void FilterItems()
        {
            IEnumerable<AuditLogDisplayModel> filtered = _allLogs;

            // 1. User Filter
            if (SelectedUser != null)
            {
                var targetUserId = SelectedUser.Id.ToString().ToLower();
                filtered = filtered.Where(l => l.Log.UserId.ToLower() == targetUserId);
            }

            // 2. Date presets / range filtering
            if (StartDate.HasValue || EndDate.HasValue)
            {
                if (StartDate.HasValue)
                {
                    var startVal = StartDate.Value.Date;
                    filtered = filtered.Where(l => l.Timestamp >= startVal);
                }
                if (EndDate.HasValue)
                {
                    var endVal = EndDate.Value.Date.AddDays(1).AddTicks(-1); // inclusive end of day
                    filtered = filtered.Where(l => l.Timestamp <= endVal);
                }
            }
            else
            {
                var nowLocal = DateTime.Now;
                switch (CurrentFilter)
                {
                    case AuditLogFilter.Daily:
                        filtered = filtered.Where(l => l.Timestamp.Date == nowLocal.Date);
                        break;
                    case AuditLogFilter.Weekly:
                        var weekStart = nowLocal.Date.AddDays(-(int)nowLocal.DayOfWeek);
                        filtered = filtered.Where(l => l.Timestamp >= weekStart);
                        break;
                    case AuditLogFilter.Monthly:
                        filtered = filtered.Where(l => l.Timestamp.Month == nowLocal.Month && l.Timestamp.Year == nowLocal.Year);
                        break;
                }
            }

            // 3. Search Query text matching
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(l =>
                    (l.UserName?.ToLower().Contains(query) ?? false) ||
                    (l.Action?.ToLower().Contains(query) ?? false) ||
                    (l.TableName?.ToLower().Contains(query) ?? false) ||
                    (l.EntityName?.ToLower().Contains(query) ?? false) ||
                    (l.NewValues?.ToLower().Contains(query) ?? false) ||
                    (l.OldValues?.ToLower().Contains(query) ?? false)
                );
            }

            var result = filtered.ToList();
            Items = new ObservableCollection<AuditLogDisplayModel>(result);

            // Compute statistics
            TotalCount = result.Count;
            CreateCount = result.Count(l => string.Equals(l.Action, "Create", StringComparison.OrdinalIgnoreCase));
            UpdateCount = result.Count(l => string.Equals(l.Action, "Update", StringComparison.OrdinalIgnoreCase));
            DeleteCount = result.Count(l => string.Equals(l.Action, "Delete", StringComparison.OrdinalIgnoreCase));
        }
    }
}
