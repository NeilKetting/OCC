using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Mobile.Features.Notifications;
using Microsoft.Extensions.DependencyInjection;
using OCC.Shared.DTOs;
using Avalonia.Media;

namespace OCC.Mobile.Features.Dashboard
{
    public class DashboardTaskViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? DaysLate { get; set; }
        public string? DueLabel { get; set; }
    }

    public class DashboardWeekGroupViewModel
    {
        public string Day { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public List<DashboardTaskViewModel> Tasks { get; set; } = new();
    }

    public class DashboardProjectBreakdownViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int Done { get; set; }
        public int Total { get; set; }
        public double Progress => Total > 0 ? (double)Done / Total * 100 : 0;
        public string StartColor { get; set; } = "#6366F1";
        public string EndColor { get; set; } = "#22D3EE";

        public IBrush ProgressBrush
        {
            get
            {
                var start = Color.Parse(StartColor);
                var end = Color.Parse(EndColor);
                return new LinearGradientBrush
                {
                    StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                    EndPoint = new Avalonia.RelativePoint(1, 0, Avalonia.RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(start, 0),
                        new GradientStop(end, 1)
                    }
                };
            }
        }
    }

    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IProjectService _projectService;
        private readonly IProjectTaskService _taskService;
        private readonly ISignalRService _signalRService;
        private readonly IAuthService _authService;
        private readonly ISiteDeploymentService _deploymentService;
        private readonly IPushNotificationService? _pushNotificationService;
        private readonly System.Threading.SemaphoreSlim _loadSemaphore = new(1, 1);

        [ObservableProperty]
        private string _pushStatus = "Initializing...";

        [ObservableProperty]
        private int _activeSitesCount;

        [ObservableProperty]
        private ObservableCollection<OCC.Shared.DTOs.DashboardUpdateDto> _recentActivity = new();

        [ObservableProperty]
        private string _greeting = string.Empty;

        [ObservableProperty]
        private int _dailyTotalTasks;

        [ObservableProperty]
        private int _dailyCompletedTasks;

        [ObservableProperty]
        private double _dailyProgress;

        [ObservableProperty]
        private double _dailyProgressAngle;

        [ObservableProperty]
        private double _dailyPendingProgressAngle;

        [ObservableProperty]
        private double _overallProgress;

        [ObservableProperty]
        private double _overallProgressAngle;

        [ObservableProperty]
        private double _overallPendingProgressAngle;

        [ObservableProperty]
        private int _overallTotalTasks;

        [ObservableProperty]
        private int _overallCompletedTasks;

        [ObservableProperty]
        private int _overdueTasksCount;

        [ObservableProperty]
        private int _pendingTasksCount;

        [ObservableProperty]
        private string _projectHealth = "On Track";

        [ObservableProperty]
        private string _projectHealthColor = "#10B981"; // Teal

        [ObservableProperty]
        private string _etaDateString = "N/A";

        [ObservableProperty]
        private string _etaStatus = "Calculating...";

        [ObservableProperty]
        private ObservableCollection<DashboardTaskViewModel> _overdueTasks = new();

        [ObservableProperty]
        private ObservableCollection<DashboardTaskViewModel> _todayTasks = new();

        [ObservableProperty]
        private ObservableCollection<DashboardWeekGroupViewModel> _weekGroups = new();

        [ObservableProperty]
        private ObservableCollection<DashboardProjectBreakdownViewModel> _projectBreakdowns = new();

        [ObservableProperty]
        private string _currentDateString = DateTime.Today.ToString("ddd dd MMM yyyy");

        public string DailyProgressMessage => DailyTotalTasks == DailyCompletedTasks
            ? "All done — great work today!"
            : $"{(DailyTotalTasks - DailyCompletedTasks)} task{((DailyTotalTasks - DailyCompletedTasks) > 1 ? "s" : "")} still need attention today.";

        public string ProjectHealthBgColor => ProjectHealthColor == "#EF4444" ? "#20EF4444" : ProjectHealthColor == "#F59E0B" ? "#20F59E0B" : "#2010B981";

        public IBrush DailyProgressBrush => new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse("#6366F1"), 0),
                new GradientStop(Color.Parse("#22D3EE"), 1)
            }
        };

        private readonly ILocalSettingsService _settingsService;

        [ObservableProperty]
        private int _pendingCrewCount;

        public DashboardViewModel(
            INavigationService navigationService,
            IProjectService projectService,
            IProjectTaskService taskService,
            ISignalRService signalRService,
            IAuthService authService,
            ISiteDeploymentService deploymentService,
            ILocalSettingsService settingsService)
        {
            _navigationService = navigationService;
            _projectService = projectService;
            _taskService = taskService;
            _signalRService = signalRService;
            _authService = authService;
            _deploymentService = deploymentService;
            _settingsService = settingsService;
            _pushNotificationService = App.Services?.GetService<IPushNotificationService>()!;
            
            PushStatus = _pushNotificationService?.Status ?? "N/A";
            if (_pushNotificationService is Features.Notifications.PushNotificationService pns)
            {
                pns.StatusChanged += (s, e) => PushStatus = e;
            }
            
            _signalRService.EntityUpdated += OnEntityUpdated;
            
            Title = "Daily Progress";
            
            // Set greeting
            var user = _authService.CurrentUser;
            Greeting = user != null ? $"Hi {user.FirstName}!" : "Hi there!";
            
            LoadData().FireAndForget();
        }

        private void OnEntityUpdated(string entityType, string action, Guid id)
        {
            if (entityType == "Project" || entityType == "ProjectTask" || entityType == "DashboardUpdate")
            {
                LoadData().FireAndForget();
            }
            // Refresh crew count if a deployment was created/received
            if (entityType == "SiteDeployment")
            {
                LoadPendingCrewCount().FireAndForget();
            }
        }

        [RelayCommand]
        private void NavigateToReceiveCrew()
        {
            _navigationService.NavigateTo<ReceiveCrewViewModel>(vm => 
            {
                vm.LoadDataCommand.Execute(null);
            });
        }

        [RelayCommand]
        private void NavigateToTask(DashboardTaskViewModel taskVm)
        {
            if (taskVm == null || !Guid.TryParse(taskVm.Id, out var taskId)) return;

            try
            {
                _navigationService.NavigateTo<Features.Tasks.RedesignTasksViewModel>(vm => 
                {
                    vm.TargetTaskId = taskId;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to navigate to redesign tasks: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            _signalRService.EntityUpdated -= OnEntityUpdated;
            base.Dispose();
        }        public async Task LoadData()
        {
            if (!await _loadSemaphore.WaitAsync(0)) return;
            try
            {
                // 1. Fetch Projects
                var projects = await _projectService.GetProjectsAsync(assignedToMe: true);
                var projectList = projects.GroupBy(p => p.Id).Select(g => g.First()).ToList(); 
                
                int dailyTotal = 0;
                int dailyCompleted = 0;
                int overdueCount = 0;
                int overallTotal = 0;
                int overallCompleted = 0;

                foreach (var p in projectList)
                {
                    // Daily stats: Tasks due today OR tasks actually completed today
                    var todayTasks = p.Tasks.Where(t => 
                        t.FinishDate.Date == DateTime.Today || 
                        (t.ActualCompleteDate.HasValue && t.ActualCompleteDate.Value.ToLocalTime().Date == DateTime.Today) ||
                        (t.IsComplete && t.UpdatedAtUtc?.ToLocalTime().Date == DateTime.Today)
                    ).ToList();
                    dailyTotal += todayTasks.Count;
                    dailyCompleted += todayTasks.Count(t => t.IsComplete);
                    
                    // Overall stats
                    overallTotal += p.Tasks.Count;
                    overallCompleted += p.Tasks.Count(t => t.IsComplete);
                    
                    overdueCount += p.Tasks.Count(t => t.IsOverdue);
                }

                var progressValue = dailyTotal > 0 ? (double)dailyCompleted / dailyTotal * 100 : 0;
                var overallProgressValue = overallTotal > 0 ? (double)overallCompleted / overallTotal * 100 : 0;
                var pendingCount = dailyTotal - dailyCompleted;
 
                // 3. Project Health & ETA Logic (Ported from WPF)
                string health = "On Track";
                string healthColor = "#10B981"; // Teal
                string etaDate = "N/A";
                string etaStat = "Waiting for progress...";
 
                if (overdueCount > 5 || (overdueCount > 0 && overallProgressValue < 20))
                {
                    health = "At Risk";
                    healthColor = "#EF4444"; // Red
                }
                else if (overdueCount > 0)
                {
                    health = "Behind Schedule";
                    healthColor = "#F59E0B"; // Amber
                }
 
                if (projectList.Any() && overallProgressValue > 0 && overallProgressValue < 100)
                {
                    var firstProject = projectList.First();
                    var startDate = firstProject.StartDate;
                    var endDate = firstProject.EndDate;
                    var now = DateTime.Now;
 
                    if (now > startDate)
                    {
                        var timeElapsed = now - startDate;
                        var totalEstimatedTicks = timeElapsed.Ticks / (overallProgressValue / 100.0);
                        var predictedEndDate = startDate.AddTicks((long)totalEstimatedTicks);
                        etaDate = predictedEndDate.ToString("dd MMM yyyy");
                        
                        var varianceDays = (predictedEndDate - endDate).TotalDays;
                        etaStat = varianceDays > 7 ? $"Expected {Math.Round(varianceDays)} days late" : "On schedule";
                    }
                }
                else if (overallProgressValue >= 100)
                {
                    etaDate = "Finished";
                    etaStat = "Project Complete";
                }

                // Query collections for redesign dashboard
                var overdueList = projectList.SelectMany(p => p.Tasks.Where(t => t.IsOverdue)
                    .Select(t => new DashboardTaskViewModel
                    {
                        Id = t.Id.ToString(),
                        Title = t.Name,
                        Project = p.Name,
                        Status = t.Status,
                        DaysLate = (DateTime.Today - t.FinishDate.Date).Days,
                        DueLabel = "Overdue"
                    }))
                    .OrderByDescending(t => t.DaysLate)
                    .ToList();

                var todayRemainingList = projectList.SelectMany(p => p.Tasks
                    .Where(t => !t.IsComplete && t.FinishDate.Date == DateTime.Today)
                    .Select(t => new DashboardTaskViewModel
                    {
                        Id = t.Id.ToString(),
                        Title = t.Name,
                        Project = p.Name,
                        Status = t.Status,
                        DueLabel = "Today"
                    }))
                    .ToList();

                var next7DaysTasks = projectList.SelectMany(p => p.Tasks
                    .Where(t => !t.IsComplete && t.FinishDate.Date > DateTime.Today && t.FinishDate.Date <= DateTime.Today.AddDays(7))
                    .Select(t => new { Task = t, Project = p }))
                    .GroupBy(x => x.Task.FinishDate.Date)
                    .OrderBy(g => g.Key)
                    .ToList();

                var weekGroupsList = new List<DashboardWeekGroupViewModel>();
                foreach (var g in next7DaysTasks)
                {
                    var dateVal = g.Key;
                    string dayLabel;
                    if (dateVal == DateTime.Today.AddDays(1))
                        dayLabel = "Tomorrow";
                    else
                        dayLabel = dateVal.ToString("dddd");

                    weekGroupsList.Add(new DashboardWeekGroupViewModel
                    {
                        Day = dayLabel,
                        Date = dateVal.ToString("ddd dd MMM"),
                        Tasks = g.Select(x => new DashboardTaskViewModel
                        {
                            Id = x.Task.Id.ToString(),
                            Title = x.Task.Name,
                            Project = x.Project.Name,
                            Status = x.Task.Status,
                            DueLabel = dateVal.ToString("ddd dd MMM")
                        }).ToList()
                    });
                }

                var gradientColors = new[]
                {
                    new { Start = "#6366F1", End = "#22D3EE" }, // Indigo to Cyan
                    new { Start = "#8B5CF6", End = "#6366F1" }, // Violet to Indigo
                    new { Start = "#EC4899", End = "#8B5CF6" }, // Pink to Violet
                };

                var projectBreakdownList = projectList.Select((p, idx) => new DashboardProjectBreakdownViewModel
                {
                    Name = p.Name,
                    Done = p.CompletedTaskCount,
                    Total = p.TotalTaskCount,
                    StartColor = gradientColors[idx % gradientColors.Length].Start,
                    EndColor = gradientColors[idx % gradientColors.Length].End
                }).ToList();

                // 2. Fetch Recent Activity
                var updates = await _taskService.GetRecentUpdatesAsync();
                var activeProjectIds = projectList.Select(p => p.Id).ToList();
                var activityList = updates
                    .Where(u => u.ProjectId.HasValue && activeProjectIds.Contains(u.ProjectId.Value))
                    .Take(10)
                    .ToList();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                {
                    ActiveSitesCount = projectList.Count;
                    DailyTotalTasks = dailyTotal;
                    DailyCompletedTasks = dailyCompleted;
                    DailyProgress = progressValue;
                    DailyProgressAngle = progressValue * 2.4; // 240 degrees horseshoe sweep
                    DailyPendingProgressAngle = (dailyTotal > 0 ? (double)pendingCount / dailyTotal * 100 : 0) * 2.4;
                    
                    OverallTotalTasks = overallTotal;
                    OverallCompletedTasks = overallCompleted;
                    OverallProgress = overallProgressValue;
                    OverallProgressAngle = overallProgressValue * 3.6;
                    OverallPendingProgressAngle = (overallTotal > 0 ? (double)(overallTotal - overallCompleted) / overallTotal * 100 : 0) * 3.6;
                    
                    OverdueTasksCount = overdueCount;
                    PendingTasksCount = pendingCount;
                    
                    ProjectHealth = health;
                    ProjectHealthColor = healthColor;
                    EtaDateString = etaDate;
                    EtaStatus = etaStat;
                    
                    CurrentDateString = DateTime.Today.ToString("ddd dd MMM yyyy");

                    OverdueTasks.Clear();
                    foreach (var t in overdueList)
                    {
                        OverdueTasks.Add(t);
                    }

                    TodayTasks.Clear();
                    foreach (var t in todayRemainingList)
                    {
                        TodayTasks.Add(t);
                    }

                    WeekGroups.Clear();
                    foreach (var wg in weekGroupsList)
                    {
                        WeekGroups.Add(wg);
                    }

                    ProjectBreakdowns.Clear();
                    foreach (var pb in projectBreakdownList)
                    {
                        ProjectBreakdowns.Add(pb);
                    }

                    RecentActivity.Clear();
                    foreach (var update in activityList)
                    {
                        RecentActivity.Add(update);
                    }

                    OnPropertyChanged(nameof(DailyProgressMessage));
                    OnPropertyChanged(nameof(ProjectHealthBgColor));
                    OnPropertyChanged(nameof(DailyProgressBrush));

                    // Cache statistics for Login screen
                    try
                    {
                        _settingsService.Settings.CachedActiveProjects = projectList.Count(p => p.IsActive);
                        _settingsService.Settings.CachedTasksToday = dailyTotal;
                        _settingsService.Settings.CachedLiveSites = projectList.Count(p => p.IsActive);
                        _settingsService.Settings.CachedTeamMembers = projectList.SelectMany(p => p.TeamMembers).Select(tm => tm.EmployeeId).Distinct().Count();
                        _settingsService.Save();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to cache dashboard stats: {ex.Message}");
                    }
                });
                LoadPendingCrewCount().FireAndForget();
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }



        [RelayCommand]
        private void NavigateToHseq()
        {
            _navigationService.NavigateTo<HSEQ.HseqListViewModel>();
        }

        private Guid? _siteManagerEmployeeId;

        private async Task<Guid?> GetSiteManagerEmployeeIdAsync()
        {
            if (_siteManagerEmployeeId.HasValue)
                return _siteManagerEmployeeId;

            if (_authService.CurrentUser == null) return null;

            try
            {
                var baseUrl = _authService.GetBaseUrl();
                using var client = new HttpClient();
                var token = _authService.CurrentToken;
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
                client.DefaultRequestHeaders.Add("X-Environment", _settingsService.Settings.SelectedEnvironment.ToString());

                var employees = await client.GetFromJsonAsync<List<EmployeeSummaryDto>>($"{baseUrl}api/Employees");
                if (employees != null)
                {
                    var currentEmployee = employees.FirstOrDefault(e => e.LinkedUserId == _authService.CurrentUser.Id);
                    if (currentEmployee != null)
                    {
                        _siteManagerEmployeeId = currentEmployee.Id;
                        return _siteManagerEmployeeId;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resolving Site Manager Employee ID: {ex.Message}");
            }

            return null;
        }

        private async Task LoadPendingCrewCount()
        {
            var smId = await GetSiteManagerEmployeeIdAsync();
            if (!smId.HasValue) return;

            try
            {
                var pending = await _deploymentService.GetPendingDeploymentsAsync(smId.Value);
                var count = pending.Count();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PendingCrewCount = count;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading pending crew count: {ex.Message}");
            }
        }
    }
}
