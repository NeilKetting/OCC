using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectDashboardViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IProjectTaskService _projectTaskService;
        private readonly ISignalRService _signalRService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<ProjectDashboardViewModel> _logger;
        private readonly INavigationService _navigationService;

        [ObservableProperty] private int _activeProjectCount;
        [ObservableProperty] private int _overdueTaskCount;
        [ObservableProperty] private double _completionRate;
        [ObservableProperty] private int _openIncidentsCount;
        [ObservableProperty] private int _activeSnagsCount;
        [ObservableProperty] private int _upcomingMilestonesCount;
        [ObservableProperty] private double _safeWorkingHours;
        [ObservableProperty] private int _activeVoCount;

        private readonly IHealthSafetyService _hseqService;
        private readonly ISnagService _snagService;
        private readonly IProjectVariationOrderService _voService;

        public ObservableCollection<DashboardUpdateDto> RecentUpdates { get; } = new();
        public ObservableCollection<ProjectTask> UpcomingMilestones { get; } = new();

        public ProjectDashboardViewModel(
            IProjectService projectService,
            IProjectTaskService projectTaskService,
            ISignalRService signalRService,
            IDialogService dialogService,
            ILogger<ProjectDashboardViewModel> logger,
            INavigationService navigationService,
            IHealthSafetyService hseqService,
            ISnagService snagService,
            IProjectVariationOrderService voService)
        {
            _projectService = projectService;
            _projectTaskService = projectTaskService;
            _signalRService = signalRService;
            _dialogService = dialogService;
            _logger = logger;
            _navigationService = navigationService;
            _hseqService = hseqService;
            _snagService = snagService;
            _voService = voService;

            Title = "Project Dashboard";
            _signalRService.DashboardUpdateReceived += OnDashboardUpdateReceived;
            _ = LoadStats();
        }

        private void OnDashboardUpdateReceived(DashboardUpdateDto update)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                RecentUpdates.Insert(0, update);
                if (RecentUpdates.Count > 10)
                {
                    RecentUpdates.RemoveAt(RecentUpdates.Count - 1);
                }
            });
        }

        private async Task LoadStats()
        {
            try
            {
                IsBusy = true;
                // Fetch stats from service
                var projects = await _projectService.GetProjectSummariesAsync();
                var projectList = projects.ToList();

                var activeProjects = projectList
                    .Where(p => p.Status == "Active" || p.Status == "Planning" || p.Status == "In Progress")
                    .ToList();

                ActiveProjectCount = activeProjects.Count;
                
                // Synchronized Overdue & Milestone Calculation
                // CRITICAL: We use _projectService.GetProjectTasksAsync to match the Project Dashboard logic exactly.
                var totalOverdue = 0;
                var allActionableTasks = new List<ProjectTask>();

                foreach (var p in activeProjects)
                {
                    try
                    {
                        // Use the SAME method as ProjectDetailViewModel.LoadProjectAsync
                        var pTasks = await _projectService.GetProjectTasksAsync(p.Id);
                        var actionable = pTasks.Where(t => !t.IsGroup).ToList();
                        
                        // Manually attach project info for the Dashboard Milestones list
                        foreach(var t in actionable)
                        {
                            t.Project = new Project { Name = p.Name };
                        }

                        totalOverdue += actionable.Count(t => t.IsOverdue);
                        allActionableTasks.AddRange(actionable);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to fetch tasks for project {p.Id}");
                    }
                }

                OverdueTaskCount = totalOverdue;
                CompletionRate = projectList.Any() ? projectList.Average(p => p.Progress) / 100.0 : 0;

                // HSEQ Stats
                var hseqStats = await _hseqService.GetDashboardStatsAsync();
                OpenIncidentsCount = hseqStats?.IncidentsTotal ?? 0;
                SafeWorkingHours = hseqStats?.TotalSafeHours ?? 0;

                // Active VOs count
                try
                {
                    var vos = await _voService.GetVariationOrdersAsync();
                    ActiveVoCount = vos.Count(v => v.Status == "Approved");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch Variation Orders count for dashboard");
                    ActiveVoCount = 0;
                }

                // Snag Stats
                var snagJobs = await _snagService.GetSnagJobsAsync();
                ActiveSnagsCount = snagJobs.Count(s => s.Status != SnagStatus.Fixed && s.Status != SnagStatus.Closed);

                // Upcoming Milestones (Next 7 days)
                var nextWeek = DateTime.Today.AddDays(7);
                var milestones = allActionableTasks.Where(t => !t.IsComplete && t.FinishDate >= DateTime.Today && t.FinishDate <= nextWeek)
                                                .OrderBy(t => t.FinishDate)
                                                .Take(5)
                                                .ToList();
                
                UpcomingMilestonesCount = allActionableTasks.Count(t => !t.IsComplete && t.FinishDate >= DateTime.Today && t.FinishDate <= nextWeek);
                UpcomingMilestones.Clear();
                foreach (var m in milestones) UpcomingMilestones.Add(m);

                var updates = await _projectTaskService.GetRecentUpdatesAsync();
                RecentUpdates.Clear();
                foreach (var u in updates)
                {
                    RecentUpdates.Add(u);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard stats");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void GoToRegistry()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Projects));
        }

        [RelayCommand]
        public void Close()
        {
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }
    }
}
