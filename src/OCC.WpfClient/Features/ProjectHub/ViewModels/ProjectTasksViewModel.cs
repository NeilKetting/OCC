using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectTasksViewModel : ViewModelBase, IOverlayProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IProjectTaskService _taskService;

        [ObservableProperty] private ObservableCollection<ProjectTask> _tasks = new();
        [ObservableProperty] private ProjectTask? _selectedTask;
        [ObservableProperty] private bool _hasTasks;
        [ObservableProperty] private TaskDetailViewModel? _currentTaskDetail;
        [ObservableProperty] private Guid _projectId;
        [ObservableProperty] private string _parentTaskName = string.Empty;

        private List<ProjectTask> _rootTasks = new();

        public ViewModelBase? ActiveOverlay => CurrentTaskDetail;

        public ProjectTasksViewModel(IServiceProvider serviceProvider, IProjectTaskService taskService)
        {
            _serviceProvider = serviceProvider;
            _taskService = taskService;
            Title = "Tasks";
        }

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
            foreach (var root in _rootTasks)
            {
                FlattenTask(root, flatList);
            }

            App.Current.Dispatcher.Invoke(() =>
            {
                Tasks = new ObservableCollection<ProjectTask>(flatList);
                HasTasks = Tasks.Any();
            });
        }

        private void FlattenTask(ProjectTask task, List<ProjectTask> flatList)
        {
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
                await vm.LoadTaskById(task.Id);
                vm.CloseFinished += (s, e) => CurrentTaskDetail = null;
                CurrentTaskDetail = vm;
            }
            catch (Exception ex)
            {
                toastService.ShowError("Error", "Could not load task details: " + ex.Message);
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
    }
}
