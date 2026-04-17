using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OCC.Shared.Interfaces;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectsViewModel : OverlayHostViewModel
    {
        private readonly IProjectService _projectService;
        private readonly ICustomerService _customerService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<ProjectsViewModel> _logger;
        private readonly IToastService _toastService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty] private ObservableCollection<ProjectSummaryDto> _projects = new();
        [ObservableProperty] private ProjectSummaryDto? _selectedProject;
        [ObservableProperty] private string _searchText = string.Empty;



        public ProjectsViewModel(
            IProjectService projectService,
            ICustomerService customerService,
            IDialogService dialogService,
            ILogger<ProjectsViewModel> logger,
            IToastService toastService,
            IServiceProvider serviceProvider)
        {
            _projectService = projectService;
            _customerService = customerService;
            _dialogService = dialogService;
            _logger = logger;
            _toastService = toastService;
            _serviceProvider = serviceProvider;

            Title = "Projects";
            _ = LoadDataAsync();
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                var projects = await _projectService.GetProjectSummariesAsync();
                Projects = new ObservableCollection<ProjectSummaryDto>(projects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project summaries");
                _toastService.ShowError("Error", "Failed to load projects");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddProject()
        {
            if (OverlayViewModel != null) return;

            var vm = new CreateProjectViewModel(
                _projectService,
                _customerService,
                _serviceProvider.GetRequiredService<IEmployeeService>(),
                _serviceProvider.GetRequiredService<IUserService>(),
                _serviceProvider.GetRequiredService<IGoogleMapsService>(),
                _serviceProvider.GetRequiredService<ISettingsService>(),
                _toastService);

            vm.CloseRequested += (s, e) => CloseOverlay();
            vm.ProjectCreated += (s, id) => { 
                CloseOverlay();
                _ = LoadDataAsync();
            };

            OpenOverlay(vm);
        }



        [RelayCommand]
        private void OpenProject(ProjectSummaryDto project)
        {
            if (project == null) return;
            WeakReferenceMessenger.Default.Send(new OpenProjectMessage(project.Id));
        }

        [RelayCommand]
        private void EditProject(ProjectSummaryDto project)
        {
            if (project == null) return;
            // TODO: Open EditProjectDialog
            _toastService.ShowInfo("Upcoming", $"Editing {project.Name} is coming soon.");
        }

        [RelayCommand]
        private async Task DeleteProject(ProjectSummaryDto project)
        {
            if (project == null) return;
            var confirm = await _dialogService.ShowConfirmationAsync("Delete Project", 
                $"Are you sure you want to delete '{project.Name}'? This action cannot be undone.");
            
            if (confirm)
            {
                // TODO: Implement deletion in service if available
                _toastService.ShowInfo("Action", "Deletion requested.");
            }
        }

        [RelayCommand]
        public void Close()
        {
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }

        partial void OnSearchTextChanged(string value)
        {
            // TODO: Implement filtering logic
        }
    }
}
