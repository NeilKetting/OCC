using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using OCC.Shared.DTOs;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        public ObservableCollection<DashboardUpdateDto> RecentUpdates { get; } = new();

        public ProjectDashboardViewModel(
            IProjectService projectService,
            IProjectTaskService projectTaskService,
            ISignalRService signalRService,
            IDialogService dialogService,
            ILogger<ProjectDashboardViewModel> logger,
            INavigationService navigationService)
        {
            _projectService = projectService;
            _projectTaskService = projectTaskService;
            _signalRService = signalRService;
            _dialogService = dialogService;
            _logger = logger;
            _navigationService = navigationService;

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

                ActiveProjectCount = projectList.Count(p => p.Status == "Active" || p.Status == "Planning");
                OverdueTaskCount = projectList.Sum(p => p.TaskCount) / 10; // Placeholder for overdue logic
                CompletionRate = projectList.Any() ? projectList.Average(p => p.Progress) / 100.0 : 0;

                var updates = await _projectTaskService.GetRecentUpdatesAsync();
                RecentUpdates.Clear();
                foreach (var u in updates)
                {
                    RecentUpdates.Add(u);
                }
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
