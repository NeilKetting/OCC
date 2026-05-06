using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OCC.WpfClient.Infrastructure;

using System.Collections.ObjectModel;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.Shared.DTOs;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectDetailViewModel : DetailViewModelBase, IRecipient<TaskUpdatedMessage>, IRecipient<ProjectUpdatedMessage>, IOverlayProvider
    {
        private readonly IProjectService _projectService;
        private readonly ProjectSpecificDashboardViewModel _dashboardVM;
        private readonly ProjectTasksViewModel _tasksVM;
        private readonly ProjectGanttViewModel _ganttVM;
        private readonly ProjectHistoryViewModel _historyVM;
        private readonly IEmployeeService _employeeService;

        [ObservableProperty] private Project? _project;
        [ObservableProperty] private ViewModelBase _currentView;
        [ObservableProperty] private Guid _projectId;
        
        [ObservableProperty] private string _siteManagerName = "Unassigned";
        [ObservableProperty] private string _projectManagerName = "Unassigned";
        [ObservableProperty] private string _siteManagerInitials = "??";
        [ObservableProperty] private bool _isSiteManagerPickerOpen;
        [ObservableProperty] private ObservableCollection<EmployeeSummaryDto> _availableSiteManagers = new();
        [ObservableProperty] private EmployeeSummaryDto? _selectedSiteManager;

        public ViewModelBase? ActiveOverlay => CurrentView;

        public ProjectDetailViewModel(
            IProjectService projectService, 
            IEmployeeService employeeService,
            ProjectSpecificDashboardViewModel dashboardVM, 
            ProjectTasksViewModel tasksVM, 
            ProjectGanttViewModel ganttVM, 
            ProjectHistoryViewModel historyVM,
            IDialogService dialogService,
            ILogger<ProjectDetailViewModel> logger,
            IPdfService pdfService) : base(dialogService, logger, pdfService)
        {
            _projectService = projectService;
            _employeeService = employeeService;
            _dashboardVM = dashboardVM;
            _tasksVM = tasksVM;
            _ganttVM = ganttVM;
            _historyVM = historyVM;
            _currentView = _dashboardVM;
            Title = "Project Detail";
            WeakReferenceMessenger.Default.Register<TaskUpdatedMessage>(this);
            WeakReferenceMessenger.Default.Register<ProjectUpdatedMessage>(this);
        }
    
        private void UpdateHeaderInfo()
        {
            if (Project == null) return;
            
            ProjectManagerName = string.IsNullOrEmpty(Project.ProjectManager) ? "Unassigned" : Project.ProjectManager;
            SiteManagerName = Project.SiteManager?.DisplayName ?? "Unassigned";
            
            // Generate initials for the circle
            if (Project.SiteManager != null)
            {
                var f = Project.SiteManager.FirstName.FirstOrDefault();
                var l = Project.SiteManager.LastName.FirstOrDefault();
                SiteManagerInitials = $"{f}{l}".ToUpper();
            }
            else
            {
                SiteManagerInitials = "SM";
            }
        }

        public async Task LoadProjectAsync(Guid projectId)
        {
            ProjectId = projectId;
            UpdateStatus("Loading project details...");
                Project = await _projectService.GetProjectAsync(projectId);
            if (Project != null)
            {
                Title = Project.Name;
                UpdateHeaderInfo();
                var tasks = await _projectService.GetProjectTasksAsync(projectId);
                _dashboardVM.UpdateProjectData(Project, tasks);
                _tasksVM.UpdateTasks(ProjectId, tasks);
                _ganttVM.UpdateTasks(ProjectId, tasks.ToList());
                _ = _historyVM.LoadHistoryAsync(ProjectId);
                UpdateStatus("Ready");
            }
        }

        [RelayCommand]
        private async Task ToggleSiteManagerPicker()
        {
            if (!IsSiteManagerPickerOpen)
            {
                var employees = await _employeeService.GetEmployeesAsync();
                AvailableSiteManagers.Clear();
                foreach (var emp in employees.Where(e => e.Status == EmployeeStatus.Active && e.Role == EmployeeRole.SiteManager))
                {
                    AvailableSiteManagers.Add(emp);
                }
            }
            IsSiteManagerPickerOpen = !IsSiteManagerPickerOpen;
        }

        [RelayCommand]
        private async Task UpdateSiteManager(EmployeeSummaryDto? employee)
        {
            if (Project == null || employee == null) return;

            IsSiteManagerPickerOpen = false;
            UpdateStatus("Updating site manager...");

            var update = new ProjectPersonnelUpdateDto
            {
                SiteManagerId = employee.Id
            };

            await _projectService.UpdateProjectPersonnelAsync(ProjectId, update);
            await LoadProjectAsync(ProjectId);
        }
    
        public void Receive(TaskUpdatedMessage message)
        {
            if (ProjectId != Guid.Empty)
            {
                _ = LoadProjectAsync(ProjectId);
            }
        }

        public void Receive(ProjectUpdatedMessage message)
        {
            if (ProjectId != Guid.Empty && (message.ProjectId == Guid.Empty || message.ProjectId == ProjectId))
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
        private void ShowHistory() => CurrentView = _historyVM;

        protected override string GetReportTitle() => $"Project Profile: {Project?.Name}";
        protected override object GetReportItem() => new
        {
            Project?.Name,
            CustomerName = Project?.Customer,
            Project?.Status,
            Project?.Priority,
            Project?.StartDate,
            Project?.EndDate,
            Project?.ProjectManager,
            SiteManager = Project?.SiteManager?.DisplayName,
            Project?.Progress
        };

        protected override async Task ExecuteSaveAsync()
        {
            if (Project == null) return;
            await _projectService.UpdateProjectAsync(Project);
        }

        protected override async Task ExecuteReloadAsync()
        {
            await LoadProjectAsync(ProjectId);
        }
    }
}
