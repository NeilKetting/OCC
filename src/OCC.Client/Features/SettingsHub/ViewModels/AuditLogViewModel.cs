using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using OCC.Shared.Models;
using System;
using System.Threading.Tasks;

using OCC.Client.ViewModels.Core;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;

namespace OCC.Client.Features.SettingsHub.ViewModels
{
    public partial class AuditLogViewModel : ViewModelBase
    {
        private readonly IAuditLogService _auditLogService;
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Employee> _employeeRepository;
        private readonly IRepository<Team> _teamRepository;
        // Task repository if needed, let's stick to these for now per request

        private List<AuditLogDisplayModel> _allLogs = new();

        [ObservableProperty]
        private ObservableCollection<AuditLogDisplayModel> _logs = new();

        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private User? _selectedUser;

        private AuditLogFilter _currentFilter = AuditLogFilter.All;

        public AuditLogViewModel(
            IAuditLogService auditLogService, 
            IRepository<User> userRepository,
            IRepository<Project> projectRepository,
            IRepository<Employee> employeeRepository,
            IRepository<Team> teamRepository)
        {
            _auditLogService = auditLogService;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
            _employeeRepository = employeeRepository;
            _teamRepository = teamRepository;
            
            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            try 
            {
                var logsTask = _auditLogService.GetAuditLogsAsync();
                var usersTask = _userRepository.GetAllAsync();
                var projectsTask = _projectRepository.GetAllAsync();
                var employeesTask = _employeeRepository.GetAllAsync();
                var teamsTask = _teamRepository.GetAllAsync();

                await Task.WhenAll(logsTask, usersTask, projectsTask, employeesTask, teamsTask);

                var logs = logsTask.Result;
                var users = usersTask.Result;
                var projects = projectsTask.Result;
                var employees = employeesTask.Result;
                var teams = teamsTask.Result;

                // Build Maps
                var userMap = users.ToDictionary(u => u.Id.ToString(), u => u.DisplayName ?? u.Email);
                var projectMap = projects.ToDictionary(p => p.Id.ToString(), p => p.Name);
                var employeeMap = employees.ToDictionary(e => e.Id.ToString(), e => $"{e.FirstName} {e.LastName}");
                var teamMap = teams.ToDictionary(t => t.Id.ToString(), t => t.Name);

                Users = new ObservableCollection<User>(users);

                _allLogs = logs.Select(l => 
                {
                    var userName = userMap.ContainsKey(l.UserId) ? userMap[l.UserId] : l.UserId;
                    
                    string entityName = l.RecordId; // Default to ID
                    string recordIdClean = l.RecordId;

                    // Clean JSON ID if present
                    if (l.RecordId.Trim().StartsWith("{") && l.RecordId.Contains("\"Id\":"))
                    {
                         try
                         {
                             // Simple string parsing to get the Guid value
                             var parts = l.RecordId.Split(new[] { "\"Id\":\"", "\"" }, StringSplitOptions.RemoveEmptyEntries);
                             // Usually {"Id":"GUID"} -> ["{", "GUID", "}"] depending on split.
                             // Let's do a cleaner json parse if needed, or simple substring
                             int start = l.RecordId.IndexOf("\"Id\":\"") + 6;
                             if (start > 6)
                             {
                                 int end = l.RecordId.IndexOf("\"", start);
                                 if (end > start)
                                 {
                                     recordIdClean = l.RecordId.Substring(start, end - start);
                                 }
                             }
                         }
                         catch { }
                    }
                    
                    // Cleanup casing for comparison
                    recordIdClean = recordIdClean.ToLower();

                    // Resolve Name based on Table
                    if (l.TableName == "Project" || l.TableName == "Projects")
                    {
                        var match = projectMap.FirstOrDefault(k => k.Key.ToLower() == recordIdClean);
                        if (!string.IsNullOrEmpty(match.Value)) entityName = match.Value;
                    }
                    else if (l.TableName == "Employee" || l.TableName == "Employees")
                    {
                        var match = employeeMap.FirstOrDefault(k => k.Key.ToLower() == recordIdClean);
                        if (!string.IsNullOrEmpty(match.Value)) entityName = match.Value;
                    }
                    else if (l.TableName == "Team" || l.TableName == "Teams")
                    {
                        var match = teamMap.FirstOrDefault(k => k.Key.ToLower() == recordIdClean);
                        if (!string.IsNullOrEmpty(match.Value)) entityName = match.Value;
                    }
                    else if (l.TableName == "User" || l.TableName == "Users")
                    {
                         var match = userMap.FirstOrDefault(k => k.Key.ToLower() == recordIdClean);
                         if (!string.IsNullOrEmpty(match.Value)) entityName = match.Value;
                    }

                    return new AuditLogDisplayModel(l, userName, entityName);
                }).OrderByDescending(l => l.Timestamp).ToList();

                ApplyFilter(_currentFilter);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        partial void OnSelectedUserChanged(User? value)
        {
            ApplyFilter(_currentFilter);
        }

        [RelayCommand]
        private void ApplyFilter(AuditLogFilter filter)
        {
            _currentFilter = filter;
            var now = DateTime.UtcNow;
            IEnumerable<AuditLogDisplayModel> filtered = _allLogs;

            // 1. Date Filter
            switch (filter)
            {
                case AuditLogFilter.Daily:
                    filtered = filtered.Where(l => l.Timestamp.Date == now.Date);
                    break;
                case AuditLogFilter.Weekly:
                    var weekStart = now.Date.AddDays(-(int)now.DayOfWeek); 
                    filtered = filtered.Where(l => l.Timestamp >= weekStart);
                    break;
                case AuditLogFilter.Monthly:
                    filtered = filtered.Where(l => l.Timestamp.Month == now.Month && l.Timestamp.Year == now.Year);
                    break;
            }

            // 2. User Filter
            if (SelectedUser != null)
            {
                // Compare IDs or Email/Name if ID mapping is inconsistent
                filtered = filtered.Where(l => l.Log.UserId == SelectedUser.Id.ToString());
            }
            
            Logs = new ObservableCollection<AuditLogDisplayModel>(filtered);
        }
    }

    public enum AuditLogFilter
    {
        All,
        Daily,
        Weekly,
        Monthly
    }
}
