using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectTasksViewModel : ViewModelBase, IOverlayProvider, IRecipient<TaskUpdatedMessage>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IProjectTaskService _taskService;
        private readonly LocalSettingsService _settingsService;

        // Column Visibility
        [ObservableProperty] private bool _isStartVisible = true;
        [ObservableProperty] private bool _isEndVisible = true;
        [ObservableProperty] private bool _isProgressVisible = true;
        [ObservableProperty] private bool _isStageVisible = true;
        [ObservableProperty] private bool _isDurationVisible = true;
        [ObservableProperty] private bool _isAssignedVisible = true;

        [ObservableProperty] private bool _isColumnPickerOpen;

        [ObservableProperty] private ObservableCollection<ProjectTask> _tasks = new();
        [ObservableProperty] private ProjectTask? _selectedTask;
        [ObservableProperty] private bool _hasTasks;
        [ObservableProperty] private TaskDetailViewModel? _currentTaskDetail;
        [ObservableProperty] private Guid _projectId;
        [ObservableProperty] private string _parentTaskName = string.Empty;

        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private string _selectedStageFilter = "All Stages";

        public ObservableCollection<string> AvailableStages { get; } = new() 
        {
            "All Stages", "Not Started", "Started", "Halfway", "Almost Done", "Completed", "On Hold"
        };

        private List<ProjectTask> _rootTasks = new();

        public ViewModelBase? ActiveOverlay => CurrentTaskDetail;

        public ProjectTasksViewModel(
            IServiceProvider serviceProvider, 
            IProjectTaskService taskService,
            LocalSettingsService settingsService)
        {
            _serviceProvider = serviceProvider;
            _taskService = taskService;
            _settingsService = settingsService;
            Title = "Tasks";
            
            LoadLayout();
            WeakReferenceMessenger.Default.Register(this);
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.ProjectTasksListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsStartVisible = layout.Columns.FirstOrDefault(c => c.Header == "Start")?.IsVisible ?? true;
                IsEndVisible = layout.Columns.FirstOrDefault(c => c.Header == "End")?.IsVisible ?? true;
                IsProgressVisible = layout.Columns.FirstOrDefault(c => c.Header == "Progress")?.IsVisible ?? true;
                IsStageVisible = layout.Columns.FirstOrDefault(c => c.Header == "Stage")?.IsVisible ?? true;
                IsDurationVisible = layout.Columns.FirstOrDefault(c => c.Header == "Duration")?.IsVisible ?? true;
                IsAssignedVisible = layout.Columns.FirstOrDefault(c => c.Header == "Assigned")?.IsVisible ?? true;
            }
        }

        private void SaveLayout()
        {
            var layout = new Features.EmployeeHub.Models.EmployeeListLayout
            {
                Columns = new List<Features.EmployeeHub.Models.ColumnConfig>
                {
                    new() { Header = "Start", IsVisible = IsStartVisible },
                    new() { Header = "End", IsVisible = IsEndVisible },
                    new() { Header = "Progress", IsVisible = IsProgressVisible },
                    new() { Header = "Stage", IsVisible = IsStageVisible },
                    new() { Header = "Duration", IsVisible = IsDurationVisible },
                    new() { Header = "Assigned", IsVisible = IsAssignedVisible }
                }
            };
            _settingsService.Settings.ProjectTasksListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsStartVisibleChanged(bool value) => SaveLayout();
        partial void OnIsEndVisibleChanged(bool value) => SaveLayout();
        partial void OnIsProgressVisibleChanged(bool value) => SaveLayout();
        partial void OnIsStageVisibleChanged(bool value) => SaveLayout();
        partial void OnIsDurationVisibleChanged(bool value) => SaveLayout();
        partial void OnIsAssignedVisibleChanged(bool value) => SaveLayout();

        [RelayCommand]
        private void ToggleColumnPicker() => IsColumnPickerOpen = !IsColumnPickerOpen;

        public void UpdateTasks(Guid projectId, IEnumerable<ProjectTask> tasks)
        {
            ProjectId = projectId;
            var taskList = tasks.ToList();

            // Build hierarchy (Ported from legacy app)
            foreach (var task in taskList) task.Children.Clear();
            
            var lookup = taskList.ToDictionary(t => t.Id);
            var roots = new List<ProjectTask>();

            foreach (var task in taskList)
            {
                if (task.ParentId.HasValue && task.ParentId != Guid.Empty && lookup.TryGetValue(task.ParentId.Value, out var parent))
                {
                    parent.Children.Add(task);
                    task.IndentLevel = parent.IndentLevel + 1;
                }
                else
                {
                    roots.Add(task);
                    task.IndentLevel = 0;
                }
            }

            _rootTasks = roots.OrderBy(t => t.OrderIndex).ToList();
            RefreshDisplayList();
        }

        private void RefreshDisplayList()
        {
            var flatList = new List<ProjectTask>();
            
            // Reapply filters to roots
            var filteredRoots = _rootTasks.Where(t => MatchesFilter(t)).ToList();
            
            foreach (var root in filteredRoots)
            {
                FlattenTask(root, flatList);
            }

            App.Current.Dispatcher.Invoke(() =>
            {
                Tasks = new ObservableCollection<ProjectTask>(flatList);
                HasTasks = Tasks.Any();
            });
        }

        private bool MatchesFilter(ProjectTask task)
        {
            bool matchesSearch = string.IsNullOrWhiteSpace(SearchQuery) || 
                task.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);

            bool matchesStage = SelectedStageFilter == "All Stages" || 
                task.Status.Equals(SelectedStageFilter, StringComparison.OrdinalIgnoreCase);

            // If this task doesn't match but a child does, we still show the parent
            bool anyChildMatches = task.Children?.Any(c => MatchesFilter(c)) ?? false;

            return (matchesSearch && matchesStage) || anyChildMatches;
        }

        private void FlattenTask(ProjectTask task, List<ProjectTask> flatList)
        {
            if (!MatchesFilter(task)) return;

            flatList.Add(task);
            if (task.IsExpanded && task.Children != null && task.Children.Any())
            {
                foreach (var child in task.Children.OrderBy(c => c.OrderIndex))
                {
                    child.IndentLevel = task.IndentLevel + 1;
                    FlattenTask(child, flatList);
                }
            }
        }

        partial void OnSearchQueryChanged(string value) => RefreshDisplayList();
        partial void OnSelectedStageFilterChanged(string value) => RefreshDisplayList();

        [RelayCommand]
        private void ToggleExpand(ProjectTask task)
        {
            if (task == null) return;
            task.IsExpanded = !task.IsExpanded;
            RefreshDisplayList();
        }

        [RelayCommand]
        private async Task NewTask()
        {
            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            try
            {
                var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
                await vm.InitializeForCreation(ProjectId);
                vm.CloseFinished += (s, e) => CurrentTaskDetail = null;
                CurrentTaskDetail = vm;
            }
            catch (Exception ex)
            {
                toastService.ShowError("Error", "Could not initialize new task: " + ex.Message);
            }
        }

        [RelayCommand]
        private async Task CreateSubtask(ProjectTask parentTask)
        {
            if (parentTask == null) return;
            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            try
            {
                var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
                await vm.InitializeForCreation(ProjectId, parentTask.Id);
                vm.CloseFinished += (s, e) => CurrentTaskDetail = null;
                CurrentTaskDetail = vm;
            }
            catch (Exception ex)
            {
                toastService.ShowError("Error", "Could not initialize sub-task: " + ex.Message);
            }
        }

        [RelayCommand]
        private async Task EditTask(ProjectTask task)
        {
            if (task == null) return;
            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            try
            {
                var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
                vm.CloseFinished += (s, e) => CurrentTaskDetail = null;
                CurrentTaskDetail = vm; // Show drawer immediately
                
                await vm.LoadTaskById(task.Id); // Load data in background
            }
            catch (Exception ex)
            {
                toastService.ShowError("Error", "Could not load task details: " + ex.Message);
                CurrentTaskDetail = null;
            }
        }

        [RelayCommand]
        private async Task DeleteTask(ProjectTask task)
        {
            if (task == null) return;
            await _taskService.DeleteTaskAsync(task.Id);
            Tasks.Remove(task);
            HasTasks = Tasks.Any();
            NotifySuccess("Task Deleted", $"Task '{task.Name}' has been removed.");
        }

        public async void Receive(TaskUpdatedMessage message)
        {
            // Find the task locally and update it
            var updatedTask = await _taskService.GetTaskAsync(message.TaskId);
            if (updatedTask == null) return;

            App.Current.Dispatcher.Invoke(() =>
            {
                var existing = Tasks.FirstOrDefault(t => t.Id == message.TaskId);
                if (existing != null)
                {
                    // Update properties manually to trigger UI refresh on the object in the list
                    existing.Status = updatedTask.Status;
                    existing.PercentComplete = updatedTask.PercentComplete;
                    existing.IsOnHold = updatedTask.IsOnHold;
                    existing.HoldReason = updatedTask.HoldReason;
                    existing.Name = updatedTask.Name;
                    existing.IsExpanded = updatedTask.IsExpanded; // Preserve or update
                    
                    // Force refresh colors and labels
                    existing.NotifyPropertyChanged(nameof(existing.StatusColor));
                    existing.NotifyPropertyChanged(nameof(existing.IsComplete));
                    existing.NotifyPropertyChanged(nameof(existing.IsOverdue));
                }
                else
                {
                    // If it's a new task or we can't find it, we might need to rebuild hierarchy
                    // But for simple updates, this is enough.
                }
            });
        }
    }
}
