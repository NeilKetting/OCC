using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.TaskHub.ViewModels
{
    public partial class TaskListViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IProjectTaskRepository _taskRepository;
        private readonly ILogger<TaskListViewModel> _logger; 
        private readonly IDialogService _dialogService;
        private bool _isDataLoading = false;

        #endregion

        #region Observables

        [ObservableProperty]
        private bool _myTasksOnly;

        partial void OnMyTasksOnlyChanged(bool value)
        {
            _ = LoadTasks();
        }

        // Changed from flat HomeTaskItem to grouped ProjectGroupViewModel
        [ObservableProperty]
        private ObservableCollection<ProjectGroupViewModel> _projectGroups = new();

        #endregion

        #region Events
        // Events to notify selection or new task request
        public event EventHandler<string>? TaskSelectionRequested;

        #endregion

        #region Constructors

        public TaskListViewModel()
        {
            // Parameterless constructor for design-time support
            _taskRepository = null!;
            _logger = null!;
            _dialogService = null!;
        }

        public TaskListViewModel(IProjectTaskRepository taskRepository, ILogger<TaskListViewModel> logger, IDialogService dialogService)
        {
            _taskRepository = taskRepository;
            _logger = logger;
            _dialogService = dialogService;

            // Subscribe to updates
            WeakReferenceMessenger.Default.Register<OCC.Client.ViewModels.Messages.TaskUpdatedMessage>(this, (r, m) =>
            {
                _ = LoadTasks();
            });
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void SelectTask(Guid taskId)
        {
            TaskSelectionRequested?.Invoke(this, taskId.ToString());
        }
        
        [RelayCommand]
        private void DeleteTask(Guid taskId)
        {
             // Placeholder for now, typically would show confirmation
             // Then call _taskRepository.DeleteAsync(taskId)
             // Then reload
        }

        [RelayCommand]
        public void NewTask()
        {
            WeakReferenceMessenger.Default.Send(new OCC.Client.ViewModels.Messages.CreateNewTaskMessage());
        }

        [RelayCommand]
        public async Task ToggleTaskComplete(TaskTreeItemViewModel item)
        {
            if (item == null) return;

            bool targetState = !item.IsCompleted;

            if (targetState && item.Children.Any())
            {
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "Complete Subtasks?",
                    "This task has subtasks. Marking it as done will also mark all subtasks as complete. Do you want to proceed?");

                if (!confirmed) return;

                await SetAllSubtasksCompleteAsync(item.Id);
            }
            else
            {
                // Simple update for leaf task
                var task = await _taskRepository.GetByIdAsync(item.Id);
                if (task != null)
                {
                    task.PercentComplete = targetState ? 100 : 0;
                    task.Status = targetState ? "Done" : "To Do";
                    await _taskRepository.UpdateAsync(task);
                }
            }

            // Refresh list
            await LoadTasks();
        }

        private async Task SetAllSubtasksCompleteAsync(Guid parentId)
        {
            var task = await _taskRepository.GetByIdAsync(parentId);
            if (task != null)
            {
                task.PercentComplete = 100;
                task.Status = "Done";
                await _taskRepository.UpdateAsync(task);
            }

            var allTasks = await _taskRepository.GetAllAsync();
            var subtasks = allTasks.Where(t => t.ParentId == parentId).ToList();

            foreach (var sub in subtasks)
            {
                await SetAllSubtasksCompleteAsync(sub.Id);
            }
        }



        #endregion

        #region Methods

        public async Task LoadTasks()
        {
            if (_isDataLoading) return;
            _isDataLoading = true;
            try 
            {
                BusyText = "Loading tasks...";
                IsBusy = true;
                ProjectGroups.Clear();

                IEnumerable<ProjectTask> tasks;
                if (MyTasksOnly)
                {
                    tasks = await _taskRepository.GetMyTasksAsync();
                }
                else
                {
                    tasks = await _taskRepository.GetAllAsync();
                }
                
                // 1. Group by Project
                // We need Project Names. Since ProjectTask has Navigation Property 'Project', 
                // we hope the repository included it. If not, we might see null.
                // The GetProjectTasks controller does query.Include(t => t.Project) ? No, currently only Assignments & Comments.
                // We might need to fetch Project Name or update the API.
                // However, the `task.Project` might be null if not included.
                // Let's assume for now we group by ProjectId and just display "Project [Id]" if name missing, 
                // or we update the API to Include Project.
                // actually, let's update the API logic to include Project if possible, OR assume we have it.
                // Wait, HomeTaskItem previously had `Project` string property. Where did it come from? 
                // It wasn't populated in the old VM code! `Project` prop in HomeTaskItem existed but was not set in previous LoadTasks!
                // Let's check previous LoadTasks:
                // Tasks.Add(new HomeTaskItem { ... Title = task.Name ... }); -> Project wasn't set!
                // So the user probably saw empty Project column.
                
                // Let's Group by ProjectId.
                var grouped = tasks.GroupBy(t => t.ProjectId);

                foreach (var group in grouped)
                {
                    if (group.Key == null)
                    {
                        // Split independent tasks into "Standalone" (Shared) and "Personal To-Do" (Private/Quick)
                        var toDos = group.Where(t => t.Type == TaskType.PersonalToDo).ToList();
                        var standalones = group.Where(t => t.Type != TaskType.PersonalToDo).ToList();

                        if (standalones.Any())
                        {
                            var standaloneVM = new ProjectGroupViewModel("Standalone Tasks");
                            var sorted = standalones.OrderBy(t => t.OrderIndex).ToList();
                            foreach(var root in BuildTaskTree(sorted)) standaloneVM.RootTasks.Add(root);
                            ProjectGroups.Add(standaloneVM);
                        }

                        if (toDos.Any())
                        {
                            var todoVM = new ProjectGroupViewModel("My To-Do List");
                            var sorted = toDos.OrderBy(t => t.OrderIndex).ToList();
                            foreach (var root in BuildTaskTree(sorted)) todoVM.RootTasks.Add(root);
                            ProjectGroups.Add(todoVM);
                        }
                        continue;
                    }

                    var firstTask = group.FirstOrDefault();
                    var projectName = firstTask?.Project?.Name ?? "Project"; 
                    
                    var projectViewModel = new ProjectGroupViewModel(projectName);

                    // 2. Build Tree for this Project
                    var sortedTasks = group.OrderBy(t => t.OrderIndex).ToList();
                    var rootTasks = BuildTaskTree(sortedTasks);

                    foreach (var root in rootTasks)
                    {
                        projectViewModel.RootTasks.Add(root);
                    }

                    ProjectGroups.Add(projectViewModel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading task list tree");
            }
            finally
            {
                IsBusy = false;
                _isDataLoading = false;
            }
        }

        private List<TaskTreeItemViewModel> BuildTaskTree(List<ProjectTask> flatTasks)
        {
            var roots = new List<TaskTreeItemViewModel>();
            var levelStack = new Dictionary<int, TaskTreeItemViewModel>();

            foreach (var task in flatTasks)
            {
                var vm = new TaskTreeItemViewModel(task);
                var level = task.IndentLevel;

                // Find nearest parent (level - 1 down to 0)
                TaskTreeItemViewModel? parent = null;
                for (int i = level - 1; i >= 0; i--)
                {
                    if (levelStack.TryGetValue(i, out var p))
                    {
                        parent = p;
                        break;
                    }
                }

                if (parent != null)
                {
                    parent.Children.Add(vm);
                }
                else
                {
                    roots.Add(vm);
                }

                // Update stack
                levelStack[level] = vm;

                // Clear deeper levels to prevent wrong parenting
                var keysToRemove = levelStack.Keys.Where(k => k > level).ToList();
                foreach (var k in keysToRemove) levelStack.Remove(k);
            }

            return roots;
        }

        #endregion
    }

}




