using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IProjectService _projectService;
        private readonly IProjectTaskService _taskService;
        private readonly ISignalRService _signalRService;
        private readonly IAuthService _authService;
        private readonly System.Threading.SemaphoreSlim _loadSemaphore = new(1, 1);

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

        public DashboardViewModel(INavigationService navigationService, IProjectService projectService, IProjectTaskService taskService, ISignalRService signalRService, IAuthService authService)
        {
            _navigationService = navigationService;
            _projectService = projectService;
            _taskService = taskService;
            _signalRService = signalRService;
            _authService = authService;
            
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
        }

        public override void Dispose()
        {
            _signalRService.EntityUpdated -= OnEntityUpdated;
            base.Dispose();
        }

        public async Task LoadData()
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
                    DailyProgressAngle = progressValue * 3.6;
                    DailyPendingProgressAngle = (dailyTotal > 0 ? (double)pendingCount / dailyTotal * 100 : 0) * 3.6;
                    
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
                    
                    RecentActivity.Clear();
                    foreach (var update in activityList)
                    {
                        RecentActivity.Add(update);
                    }
                });
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        [RelayCommand]
        private void NavigateToMyTasks()
        {
            _navigationService.NavigateTo<MyTasksViewModel>();
        }

        [RelayCommand]
        private void NavigateToHseq()
        {
            _navigationService.NavigateTo<HSEQ.HseqListViewModel>();
        }
    }
}
