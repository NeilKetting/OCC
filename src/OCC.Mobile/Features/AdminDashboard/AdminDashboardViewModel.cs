using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using System.Collections.ObjectModel;

namespace OCC.Mobile.Features.AdminDashboard
{
    public partial class AdminDashboardViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IProjectService _projectService;
        private readonly IProjectTaskService _taskService;
        private readonly ISignalRService _signalRService;

        [ObservableProperty]
        private string _activeProjectsCount = "0";

        [ObservableProperty]
        private string _overdueTasksCount = "0";

        [ObservableProperty]
        private string _avgCompletion = "0%";

        [ObservableProperty]
        private double _completionProgress = 0;

        public ObservableCollection<ActivityItem> RecentUpdates { get; } = new();

        public AdminDashboardViewModel(
            INavigationService navigationService, 
            IProjectService projectService,
            IProjectTaskService taskService,
            ISignalRService signalRService)
        {
            _navigationService = navigationService;
            _projectService = projectService;
            _taskService = taskService;
            _signalRService = signalRService;
            
            _signalRService.EntityUpdated += OnEntityUpdated;
            
            Title = "Admin Dashboard";
            LoadRealData();
        }

        private void OnEntityUpdated(string entityType, string action, Guid id)
        {
            if (entityType == "Project" || entityType == "ProjectTask")
            {
                LoadRealData();
            }
        }

        public override void Dispose()
        {
            _signalRService.EntityUpdated -= OnEntityUpdated;
            base.Dispose();
        }

        [RelayCommand]
        private void NavigateToActiveProjects()
        {
            _navigationService.NavigateTo<ActiveProjectsViewModel>();
        }

        [RelayCommand]
        private void NavigateToOverdueTasks()
        {
            _navigationService.NavigateTo<OverdueTasksViewModel>();
        }

        private async void LoadRealData()
        {
            IsBusy = true;
            try
            {
                var allProjects = (await _projectService.GetProjectsAsync(assignedToMe: true)).ToList();
                var activeProjects = allProjects.Where(p => p.Status != "Completed" && p.Status != "Done" && p.Status != "Cancelled").ToList();
                ActiveProjectsCount = activeProjects.Count.ToString();
                
                var tasks = (await _taskService.GetTasksAsync()).ToList();
                var overdue = tasks.Count(t => t.IsOverdue);
                OverdueTasksCount = overdue.ToString();

                if (allProjects.Any())
                {
                    // Basic progress calc: average of all project tasks if available
                    // or just use a placeholder for now if tasks are not fully loaded
                    double totalProgress = 0;
                    int projectWithTasks = 0;
                    foreach(var p in allProjects)
                    {
                        if (p.Tasks.Any())
                        {
                            totalProgress += p.Tasks.Average(t => (double)t.PercentComplete);
                            projectWithTasks++;
                        }
                    }
                    
                    if (projectWithTasks > 0)
                    {
                        CompletionProgress = (totalProgress / projectWithTasks) / 100.0;
                        AvgCompletion = $"{(int)(CompletionProgress * 100)}%";
                    }
                }

                // Load Recent Updates
                var updates = await _taskService.GetRecentUpdatesAsync();
                RecentUpdates.Clear();
                foreach (var update in updates.Take(5))
                {
                    RecentUpdates.Add(new ActivityItem(
                        update.Message, 
                        update.ProjectName, 
                        update.Timestamp.ToString("yyyy-MM-dd HH:mm"), 
                        update.StatusColor));
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public class ActivityItem
    {
        public string Title { get; }
        public string Subtitle { get; }
        public string Timestamp { get; }
        public string Color { get; }

        public ActivityItem(string title, string subtitle, string timestamp, string color)
        {
            Title = title;
            Subtitle = subtitle;
            Timestamp = timestamp;
            Color = color;
        }
    }
}
