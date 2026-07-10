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
    public partial class ProjectDetailViewModel : DetailViewModelBase, IRecipient<TaskUpdatedMessage>, IRecipient<ProjectUpdatedMessage>, IRecipient<CreateTaskFromVariationOrderMessage>, IRecipient<ProjectDashboardNavigationMessage>, IOverlayProvider
    {
        private readonly IProjectService _projectService;
        private readonly ProjectSpecificDashboardViewModel _dashboardVM;
        private readonly ProjectTaskListViewModel _tasksVM;
        private readonly ProjectGanttViewModel _ganttVM;
        private readonly ProjectHistoryListViewModel _historyVM;
        private readonly IEmployeeService _employeeService;
        private readonly ProjectReportViewModel _reportVM;
        private readonly ProjectVariationOrderListViewModel _variationOrdersVM;
        private readonly ProjectHseqViewModel _projectHseqVM;
        private readonly CrewDeploymentListViewModel _crewDeploymentVM;

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
            ProjectTaskListViewModel tasksVM, 
            ProjectGanttViewModel ganttVM, 
            ProjectHistoryListViewModel historyVM,
            ProjectReportViewModel reportVM,
            ProjectVariationOrderListViewModel variationOrdersVM,
            ProjectHseqViewModel projectHseqVM,
            CrewDeploymentListViewModel crewDeploymentVM,
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
            _reportVM = reportVM;
            _variationOrdersVM = variationOrdersVM;
            _projectHseqVM = projectHseqVM;
            _crewDeploymentVM = crewDeploymentVM;
            _currentView = _dashboardVM;
            Title = "Project Detail";
            WeakReferenceMessenger.Default.Register<TaskUpdatedMessage>(this);
            WeakReferenceMessenger.Default.Register<ProjectUpdatedMessage>(this);
            WeakReferenceMessenger.Default.Register<CreateTaskFromVariationOrderMessage>(this);
            WeakReferenceMessenger.Default.Register<ProjectDashboardNavigationMessage>(this);
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

        public async Task LoadProjectAsync(Guid projectId, bool silent = false)
        {
            ProjectId = projectId;
            
            try
            {
                if (!silent)
                {
                    IsBusy = true;
                    BusyText = "Loading project details...";
                    UpdateStatus("Loading project details...");
                }
                
                Project = await _projectService.GetProjectAsync(projectId);
                if (Project != null)
                {
                    Title = Project.Name;
                    UpdateHeaderInfo();
                    var tasks = await _projectService.GetProjectTasksAsync(projectId);
                    _dashboardVM.UpdateProjectData(Project, tasks);
                    await _tasksVM.UpdateTasksAsync(ProjectId, tasks, silent);
                    await _ganttVM.UpdateTasksAsync(ProjectId, tasks.ToList(), silent);
                    _ = _historyVM.LoadHistoryAsync(ProjectId, silent);
                    await _reportVM.LoadReportDataAsync(ProjectId, autoGenerate: true, silent: silent);
                    await _variationOrdersVM.LoadProjectAsync(ProjectId, silent);
                    _projectHseqVM.Initialize(ProjectId, silent);
                    _crewDeploymentVM.Initialize(ProjectId, Project.Name);
                    if (!silent) UpdateStatus("Ready");
                }
            }
            finally
            {
                if (!silent) IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ToggleSiteManagerPicker()
        {
            if (!IsSiteManagerPickerOpen)
            {
                var employees = await _employeeService.GetEmployeesAsync();
                AvailableSiteManagers.Clear();
                foreach (var emp in employees.Where(e => e.Status == EmployeeStatus.Active && 
                                                        (e.Role == EmployeeRole.SiteManager || 
                                                          e.Role == EmployeeRole.SnrForeman || 
                                                          e.Role == EmployeeRole.JnrForeman || 
                                                          e.Role == EmployeeRole.LegacySeniorForeman)))
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
                _ = LoadProjectAsync(ProjectId, silent: true);
            }
        }

        public void Receive(ProjectUpdatedMessage message)
        {
            if (ProjectId != Guid.Empty && (message.ProjectId == Guid.Empty || message.ProjectId == ProjectId))
            {
                _ = LoadProjectAsync(ProjectId, silent: true);
            }
        }

        public void Receive(CreateTaskFromVariationOrderMessage message)
        {
            ShowTasks();
        }

        public void Receive(ProjectDashboardNavigationMessage message)
        {
            if (message.TargetView == "Tasks")
            {
                CurrentView = _tasksVM;
                if (message.Filter == "Completed")
                {
                    _tasksVM.ApplyFilters("Completed", "All Tasks");
                }
                else if (message.Filter == "InProgress")
                {
                    _tasksVM.ApplyFilters("Started", "All Tasks");
                }
                else if (message.Filter == "Overdue")
                {
                    _tasksVM.ApplyFilters("All Stages", "Overdue");
                }
                else
                {
                    _tasksVM.ApplyFilters("All Stages", "All Tasks");
                }
            }
            else if (message.TargetView == "Safety")
            {
                ShowProjectHseq();
            }
            else if (message.TargetView == "VariationOrders")
            {
                ShowVariationOrders();
            }
        }

        [RelayCommand]
        private void ShowDashboard() => CurrentView = _dashboardVM;

        [RelayCommand]
        private void ShowTasks() => CurrentView = _tasksVM;

        [RelayCommand]
        private async Task ShowGantt()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading Gantt Chart...";
                
                // Allow the busy spinner to render before blocking the thread with layout
                await Task.Delay(100);
                
                CurrentView = _ganttVM;
                
                // Wait for the UI layout and rendering cycle to complete
                await App.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ShowHistory() => CurrentView = _historyVM;

        [RelayCommand]
        private void ShowReport() => CurrentView = _reportVM;

        [RelayCommand]
        private void ShowVariationOrders() => CurrentView = _variationOrdersVM;

        [RelayCommand]
        private void ShowProjectHseq()
        {
            CurrentView = _projectHseqVM;
        }

        [RelayCommand]
        private void ShowCrewDeployments()
        {
            CurrentView = _crewDeploymentVM;
            _ = _crewDeploymentVM.LoadDeploymentsAsync();
        }
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

        public void SelectTask(Guid taskId)
        {
            CurrentView = _tasksVM;
            var task = _tasksVM.Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                _tasksVM.SelectedTask = task;
            }
        }

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
