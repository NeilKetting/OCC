using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using OCC.Client.Services;
using OCC.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using OCC.Client.Features.HomeHub.ViewModels.Shared;
using System;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OCC.Client.Services.Repositories.ApiServices;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;

using OCC.Client.ViewModels.Messages;
namespace OCC.Client.Features.HomeHub.ViewModels.Dashboard
{
    public partial class TasksWidgetViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IProjectTaskRepository _taskRepository;
        private readonly ILogger<TasksWidgetViewModel> _logger;
        private readonly IDialogService _dialogService;

        #endregion

        #region Observables

        [ObservableProperty]
        private ObservableCollection<HomeTaskItem> _todayTasks = new();

        [ObservableProperty]
        private ObservableCollection<HomeTaskItem> _upcomingTasks = new();

        [ObservableProperty]
        private ObservableCollection<HomeTaskItem> _todayToDos = new();

        [ObservableProperty]
        private ObservableCollection<HomeTaskItem> _upcomingToDos = new();

        [ObservableProperty]
        private int _todayTaskCount;

        [ObservableProperty]
        private int _upcomingTaskCount;

        [ObservableProperty]
        private int _todayToDoCount;

        [ObservableProperty]
        private int _upcomingToDoCount;

        #endregion

        #region Constructors

        public TasksWidgetViewModel()
        {
            // Parameterless constructor for design-time support
            _taskRepository = null!;
            _logger = null!;
            _dialogService = null!;
        }
        
        public TasksWidgetViewModel(IProjectTaskRepository taskRepository, ILogger<TasksWidgetViewModel> logger, IDialogService dialogService)
        {
            _taskRepository = taskRepository;
            _logger = logger;
            _dialogService = dialogService;
            LoadTasks();
            
            // Subscribe to updates
            WeakReferenceMessenger.Default.Register<OCC.Client.ViewModels.Messages.TaskUpdatedMessage>(this, (r, m) => LoadTasks());
        }

        #endregion

        #region Methods

        private async void LoadTasks()
        {
            try
            {
                IEnumerable<ProjectTask> allTasks = await _taskRepository.GetMyTasksAsync();
                var now = DateTime.Today;
                var weekEnd = now.AddDays(7);

                // Clear Collections
                TodayTasks.Clear();
                UpcomingTasks.Clear();
                TodayToDos.Clear();
                UpcomingToDos.Clear();

                foreach (var task in allTasks.OrderBy(t => t.FinishDate))
                {
                    if (task.IsComplete) continue;

                    var item = new HomeTaskItem
                    {
                        Id = task.Id,
                        Title = task.Name,
                        Description = task.Description,
                        DueDate = task.FinishDate,
                        Status = task.Status,
                        Priority = task.Priority,
                        AssigneeInitials = task.AssigneeInitials
                    };

                    bool isTodayOrOverdue = task.FinishDate.Date <= now;
                    bool isUpcoming = task.FinishDate.Date > now;

                    if (task.Type == TaskType.PersonalToDo)
                    {
                        if (isTodayOrOverdue) TodayToDos.Add(item);
                        else if (isUpcoming) UpcomingToDos.Add(item);
                    }
                    else
                    {
                        if (isTodayOrOverdue) TodayTasks.Add(item);
                        else if (isUpcoming) UpcomingTasks.Add(item);
                    }
                }

                TodayTaskCount = TodayTasks.Count;
                UpcomingTaskCount = UpcomingTasks.Count;
                TodayToDoCount = TodayToDos.Count;
                UpcomingToDoCount = UpcomingToDos.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tasks widget");
            }
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task OpenTask(HomeTaskItem item)
        {
            if (item == null) return;
            WeakReferenceMessenger.Default.Send(new OCC.Client.ViewModels.Messages.TaskSelectedMessage(item.Id));
            await System.Threading.Tasks.Task.CompletedTask;
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task CompleteTask(HomeTaskItem item)
        {
            if (item == null) return;
            try
            {
                var task = await _taskRepository.GetByIdAsync(item.Id);
                if (task != null)
                {
                    task.Status = "Completed";
                    task.PercentComplete = 100;
                    task.FinishDate = DateTime.Now;
                    await _taskRepository.UpdateAsync(task);
                    
                    WeakReferenceMessenger.Default.Send(new OCC.Client.ViewModels.Messages.TaskUpdatedMessage(task.Id));
                    LoadTasks();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing task {TaskId}", item.Id);
            }
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task DeleteTask(HomeTaskItem item)
        {
            if (item == null) return;
            try
            {
                var task = await _taskRepository.GetByIdAsync(item.Id);
                if (task != null)
                {
                    await _taskRepository.DeleteAsync(task.Id);
                    
                    WeakReferenceMessenger.Default.Send(new OCC.Client.ViewModels.Messages.TaskUpdatedMessage(task.Id));
                    LoadTasks();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task {TaskId}", item.Id);
            }
        }

        #endregion
    }
}
