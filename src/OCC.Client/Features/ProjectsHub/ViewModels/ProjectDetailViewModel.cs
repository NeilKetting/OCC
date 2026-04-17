using OCC.Client.Features.TaskHub.Views;
using OCC.Client.Features.TaskHub.ViewModels;
using OCC.Client.ModelWrappers;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.Models;
using System;
using OCC.Client.Services;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.Messages;
using OCC.Client.ViewModels.Messages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OCC.Client.Infrastructure;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectDetailViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IProjectManager _projectManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly System.Threading.CancellationTokenSource _cts = new();
        private readonly System.Threading.SemaphoreSlim _loadLock = new(1, 1);
        private List<ProjectTask> _rootTasks = new();

        #endregion

        #region Events

        public event EventHandler<Guid>? TaskSelectionRequested;

        #endregion

        #region Observables

        [ObservableProperty]
        private ProjectTopBarViewModel _topBar;

        [ObservableProperty]
        private ViewModelBase _currentView;

        [ObservableProperty]
        private ProjectTaskListViewModel _listVM;

        [ObservableProperty]
        private ProjectGanttViewModel _ganttVM;

        [ObservableProperty]
        private ProjectDashboardViewModel _dashboardVM;
        
        [ObservableProperty]
        private ProjectOverdueTasksViewModel _overdueTasksVM;
        
        [ObservableProperty]
        private ProjectVariationOrderListViewModel _variationOrderVM;

        [ObservableProperty]
        private ProjectFilesViewModel _filesVM;

        [ObservableProperty]
        private ProjectCustomerReportViewModel _reportVM;

        [ObservableProperty]
        private TaskDetailViewModel? _selectedTaskDetailVM;

        [ObservableProperty]
        private bool _isTaskDetailOpen;

        [ObservableProperty]
        private bool _isPinned;

        [ObservableProperty]
        private EditProjectViewModel? _editProjectVM;

        [ObservableProperty]
        private bool _isEditProjectVisible;

        [ObservableProperty]
        private Guid _currentProjectId;

        #endregion

        #region Properties

        public bool HasTasks => ListVM.HasTasks;

        #endregion

        #region Constructors

        /// <summary>
        /// Design-time constructor
        /// </summary>
        public ProjectDetailViewModel()
        {
            _projectManager = null!;
            _serviceProvider = null!;
            _dialogService = null!;
            _toastService = null!;
            _topBar = new ProjectTopBarViewModel();
            _listVM = new ProjectTaskListViewModel();
            _ganttVM = new ProjectGanttViewModel();
            _dashboardVM = new ProjectDashboardViewModel();
            _overdueTasksVM = new ProjectOverdueTasksViewModel();
            _variationOrderVM = new ProjectVariationOrderListViewModel();
            _filesVM = new ProjectFilesViewModel();
            _reportVM = new ProjectCustomerReportViewModel();
            _currentView = _listVM;
        }
        
        public ProjectDetailViewModel(IProjectManager projectManager, IServiceProvider serviceProvider)
        {
            _projectManager = projectManager;
            _serviceProvider = serviceProvider;
            _dialogService = serviceProvider.GetRequiredService<IDialogService>();
            _toastService = serviceProvider.GetRequiredService<IToastService>();

            _topBar = serviceProvider.GetRequiredService<ProjectTopBarViewModel>();
            _listVM = new ProjectTaskListViewModel();
            _ganttVM = new ProjectGanttViewModel(_projectManager);
            _dashboardVM = new ProjectDashboardViewModel();
            _overdueTasksVM = new ProjectOverdueTasksViewModel();
            _variationOrderVM = new ProjectVariationOrderListViewModel(serviceProvider.GetRequiredService<IProjectVariationOrderService>(), _toastService);
            _filesVM = serviceProvider.GetRequiredService<ProjectFilesViewModel>();
            _reportVM = serviceProvider.GetRequiredService<ProjectCustomerReportViewModel>();

            _currentView = _dashboardVM;

            _listVM.TaskSelectionRequested += (s, id) => OnTaskSelectionRequested(id);
            _listVM.ToggleExpandRequested += (s, e) => { RefreshDisplayList(); };

            _dashboardVM.ViewOverdueTasksRequested += (s, tasks) => 
            {
                _overdueTasksVM.LoadTasks(tasks);
                CurrentView = _overdueTasksVM;
            };

            _overdueTasksVM.BackRequested += (s, e) =>
            {
                CurrentView = _dashboardVM;
            };

            _overdueTasksVM.TaskSelectionRequested += (s, task) =>
            {
                OpenTaskDetail(task);
            };

            _topBar.PropertyChanged += TopBar_PropertyChanged;
            _topBar.DeleteProjectRequested += OnDeleteProjectRequested;
            _topBar.EditProjectRequested += OnEditProjectRequested;
            
            WeakReferenceMessenger.Default.Register<OCC.Client.ViewModels.Messages.TaskUpdatedMessage>(this, (r, m) =>
            {
                if (CurrentProjectId != Guid.Empty)
                {
                    LoadTasks(CurrentProjectId);
                }
            });
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void PreviewTaskDetail(ProjectTask task)
        {
            // Cancel any pending close
            _previewCancellation?.Cancel();
            _previewCancellation = new System.Threading.CancellationTokenSource();

            LoadTaskDetail(task, pin: false);
        }

        [RelayCommand]
        private void PinTaskDetail(ProjectTask task)
        {
            // Cancel any pending close
            _previewCancellation?.Cancel();
            _previewCancellation = new System.Threading.CancellationTokenSource();

            LoadTaskDetail(task, pin: true);
        }

        private System.Threading.CancellationTokenSource? _previewCancellation;

        [RelayCommand]
        private void EndPreview()
        {
            if (IsPinned) return;

            if (!IsPinned && IsTaskDetailOpen)
            {
                IsTaskDetailOpen = false;
                SelectedTaskDetailVM = null;
            }
        }

        private bool _isLoadingTaskDetail;

        private async void LoadTaskDetail(ProjectTask task, bool pin)
        {
            if (task == null) return; 

            if (_isLoadingTaskDetail) return; // Prevent double clicks
            
            // Optimization: If already loaded same task, just update pin status
            if (SelectedTaskDetailVM != null && SelectedTaskDetailVM.Task?.Id == task.Id)
            {
                if (pin) IsPinned = true;
                IsTaskDetailOpen = true;
                return;
            }

            try 
            {
                _isLoadingTaskDetail = true;
                var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
                
                // Show immediately so the built-in loading spinner in TaskDetailView is visible
                SelectedTaskDetailVM = vm;
                IsPinned = pin;
                IsTaskDetailOpen = true;

                if (task.Id == Guid.Empty)
                {
                    await vm.InitializeForCreation(CurrentProjectId);
                }
                else
                {
                    await vm.LoadTaskModel(task);
                }
                vm.CloseRequested += TaskDetailVM_CloseRequested;
            }
            finally
            {
                _isLoadingTaskDetail = false;
            }
        }

        private void TaskDetailVM_CloseRequested(object? sender, EventArgs e)
        {
            CloseTaskDetail();
        }

        [RelayCommand]
        private void CloseTaskDetail()
        {
            _previewCancellation?.Cancel();
            IsTaskDetailOpen = false;
            SelectedTaskDetailVM = null;
            IsPinned = false;
        }

        [RelayCommand]
        private void OpenTaskDetail(ProjectTask task)
        {
            // Legacy/Fallback - treats as Pin
            PinTaskDetail(task);
        }

        [RelayCommand]
        private void AddNewTask()
        {
            if (CurrentProjectId == Guid.Empty) return;
            OpenNewTaskPopup();
        }

        private async void OpenNewTaskPopup()
        {
            if (CurrentProjectId == Guid.Empty) return;

            var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
            await vm.InitializeForCreation(CurrentProjectId);
            vm.CloseRequested += TaskDetailVM_CloseRequested;
            
            SelectedTaskDetailVM = vm;
            IsPinned = true;
            IsTaskDetailOpen = true;
        }



        [RelayCommand]
        private void ToggleExpand(ProjectTask task)
        {
            if (task == null) return;
            _projectManager.ToggleExpand(task);
            RefreshDisplayList();
        }

        [RelayCommand]
        private async Task DeleteTask(ProjectTask task)
        {
            if (task == null) return;

            var confirm = await _dialogService.ShowConfirmationAsync("Delete Task", $"Are you sure you want to delete '{task.Name}'?");
            if (confirm)
            {
                try
                {
                    await _projectManager.DeleteTaskAsync(task.Id);
                    if (SelectedTaskDetailVM?.Task?.Id == task.Id)
                    {
                        CloseTaskDetail();
                    }
                    LoadTasks(CurrentProjectId);
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowAlertAsync("Error", $"Failed to delete task: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void OpenTask(ProjectTask task)
        {
            if (task == null) return;
            PinTaskDetail(task); // Reusing existing logic to open/pin the task detail overlay
        }

        private async void OnEditProjectRequested(object? sender, EventArgs e)
        {
            if (CurrentProjectId == Guid.Empty) return;

            var project = await _projectManager.GetProjectByIdAsync(CurrentProjectId);
            if (project == null) return;

            EditProjectVM = _serviceProvider.GetRequiredService<EditProjectViewModel>();

            EditProjectVM.LoadProject(project);
            EditProjectVM.CloseRequested += (s, ev) => IsEditProjectVisible = false;
            EditProjectVM.ProjectUpdated += (s, ev) => LoadTasks(CurrentProjectId);

            IsEditProjectVisible = true;
        }

        #endregion

        #region Methods

        public async void LoadTasks(Guid projectId)
        {
            await _loadLock.WaitAsync();
            try
            {
                BusyText = "Loading project data...";
                IsBusy = true;
                CurrentProjectId = projectId;
                
                var project = await _projectManager.GetProjectByIdAsync(projectId);
                var managers = await _projectManager.GetSiteManagersAsync();
                var tasks = await _projectManager.GetTasksForProjectAsync(projectId);

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (project != null)
                    {
                        await TopBar.LoadProjectDataAsync(project);
                    }

                    _rootTasks = _projectManager.BuildTaskHierarchy(tasks);
                    RefreshDisplayList();
                    GanttVM.LoadTasks(projectId);
                    DashboardVM.UpdateProjectData(project, tasks);
                    OverdueTasksVM.LoadTasks(tasks); // Ensure realtime UI updates
                    VariationOrderVM.LoadProject(projectId);
                    _ = FilesVM.LoadProjectFilesAsync(projectId);

                    var variationList = VariationOrderVM.VariationOrders?.ToList() ?? new List<ProjectVariationOrderWrapper>();
                    if (project != null)
                    {
                        await ReportVM.LoadReportDataAsync(project, new ObservableCollection<ProjectTask>(tasks), new ObservableCollection<ProjectVariationOrderWrapper>(variationList));
                    }
                });
            }
            finally
            {
                IsBusy = false;
                _loadLock.Release();
            }
        }

        private void OnTaskSelectionRequested(Guid taskId)
        {
            TaskSelectionRequested?.Invoke(this, taskId);
        }

        private void TopBar_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectTopBarViewModel.ActiveTab))
            {
                switch (TopBar.ActiveTab)
                {
                    case "List":
                        CurrentView = ListVM;
                        break;
                    case "Gantt":
                        CurrentView = GanttVM;
                        break;
                    case "Dashboard":
                        CurrentView = DashboardVM;
                        break;
                    case "Sheet":
                        CurrentView = VariationOrderVM;
                        break;
                    case "Report":
                        CurrentView = ReportVM;
                        break;
                    case "Files":
                        CurrentView = FilesVM;
                        break;
                }
            }
        }

        private void RefreshDisplayList()
        {
            var flatList = _projectManager.FlattenHierarchy(_rootTasks);
            ListVM.UpdateTasks(flatList);
            OnPropertyChanged(nameof(HasTasks));
        }



        private async void OnDeleteProjectRequested(object? sender, EventArgs e)
        {
            if (CurrentProjectId == Guid.Empty) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Project",
                $"Are you sure you want to delete '{TopBar.ProjectName}'? This will permanently remove all tasks, comments, and assignments. This action cannot be undone.");

            if (confirmed)
            {
                try
                {
                    BusyText = "Deleting project...";
                    IsBusy = true;
                    await _projectManager.DeleteProjectAsync(CurrentProjectId);
                    _toastService.ShowSuccess("Project Deleted", "The project has been successfully removed.");
                    WeakReferenceMessenger.Default.Send(new NavigationRequestMessage(OCC.Client.Infrastructure.NavigationRoutes.Projects)); 
                    WeakReferenceMessenger.Default.Send(new SwitchTabMessage("Projects"));
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowAlertAsync("Deletion Failed", $"Could not delete project: {ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private string GetInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "P";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }

        #endregion
    }
}




