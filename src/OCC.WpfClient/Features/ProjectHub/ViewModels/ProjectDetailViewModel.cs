using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OCC.WpfClient.Infrastructure;

using OCC.WpfClient.Infrastructure.Messages;
using CommunityToolkit.Mvvm.Messaging;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectDetailViewModel : ViewModelBase, IRecipient<TaskUpdatedMessage>, IOverlayProvider
    {
        private readonly IProjectService _projectService;
        private readonly ProjectSpecificDashboardViewModel _dashboardVM;
        private readonly ProjectTasksViewModel _tasksVM;
        private readonly ProjectGanttViewModel _ganttVM;
        private readonly SubContractorListViewModel _subContractorsVM;

        [ObservableProperty] private Project? _project;
        [ObservableProperty] private ViewModelBase _currentView;
        [ObservableProperty] private Guid _projectId;

        public ViewModelBase? ActiveOverlay => CurrentView;

        public ProjectDetailViewModel(IProjectService projectService, ProjectSpecificDashboardViewModel dashboardVM, ProjectTasksViewModel tasksVM, ProjectGanttViewModel ganttVM, SubContractorListViewModel subContractorsVM)
        {
            _projectService = projectService;
            _dashboardVM = dashboardVM;
            _tasksVM = tasksVM;
            _ganttVM = ganttVM;
            _subContractorsVM = subContractorsVM;
            _currentView = _dashboardVM;
            Title = "Project Detail";
            WeakReferenceMessenger.Default.Register<TaskUpdatedMessage>(this);
        }

        public async Task LoadProjectAsync(Guid projectId)
        {
            ProjectId = projectId;
            UpdateStatus("Loading project details...");
            Project = await _projectService.GetProjectAsync(projectId);
            if (Project != null)
            {
                Title = Project.Name;
                var tasks = await _projectService.GetProjectTasksAsync(projectId);
                _dashboardVM.UpdateProjectData(Project, tasks);
                _tasksVM.UpdateTasks(ProjectId, tasks);
                _ganttVM.UpdateTasks(ProjectId, tasks.ToList());
                UpdateStatus("Ready");
            }
        }

        public void Receive(TaskUpdatedMessage message)
        {
            if (ProjectId != Guid.Empty)
            {
                _ = LoadProjectAsync(ProjectId);
            }
        }

        [RelayCommand]
        private void ShowDashboard() => CurrentView = _dashboardVM;

        [RelayCommand]
        private void ShowTasks() => CurrentView = _tasksVM;

        [RelayCommand]
        private void ShowGantt() => CurrentView = _ganttVM;

        [RelayCommand]
        private void ShowSubContractors() => CurrentView = _subContractorsVM;
    }
}
