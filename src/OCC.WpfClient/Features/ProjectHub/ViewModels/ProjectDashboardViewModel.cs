using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using OCC.Shared.DTOs;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectDashboardViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<ProjectDashboardViewModel> _logger;
        private readonly INavigationService _navigationService;

        [ObservableProperty] private int _activeProjectCount;
        [ObservableProperty] private int _overdueTaskCount;
        [ObservableProperty] private double _completionRate;

        public ProjectDashboardViewModel(
            IProjectService projectService,
            IDialogService dialogService,
            ILogger<ProjectDashboardViewModel> logger,
            INavigationService navigationService)
        {
            _projectService = projectService;
            _dialogService = dialogService;
            _logger = logger;
            _navigationService = navigationService;

            Title = "Project Dashboard";
            _ = LoadStats();
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
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void GoToRegistry()
        {
            _navigationService.NavigateTo(NavigationRoutes.Projects);
        }

        [RelayCommand]
        public void Close()
        {
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }
    }
}
