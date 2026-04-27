using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class TaskDetailViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IProjectTaskService _taskService;

        [ObservableProperty]
        private ProjectTask _task;

        [ObservableProperty]
        private string _status;

        [ObservableProperty]
        private double _percentComplete;

        [ObservableProperty]
        private bool _isOnHold;

        [ObservableProperty]
        private string _holdReason;

        public List<string> Statuses { get; } = new() { "Not Started", "Started", "Halfway", "Almost Done", "Completed", "On Hold" };

        public TaskDetailViewModel(INavigationService navigationService, IProjectTaskService taskService)
        {
            _navigationService = navigationService;
            _taskService = taskService;
            // Design time or default
            _task = new ProjectTask { Name = "Task Detail" };
            _status = _task.Status;
            _percentComplete = _task.PercentComplete;
            _isOnHold = _task.IsOnHold;
            _holdReason = _task.HoldReason;
        }

        public TaskDetailViewModel(INavigationService navigationService, IProjectTaskService taskService, ProjectTask task)
        {
            _navigationService = navigationService;
            _taskService = taskService;
            _task = task;
            _status = task.Status;
            _percentComplete = task.PercentComplete;
            _isOnHold = task.IsOnHold;
            _holdReason = task.HoldReason;
            Title = "Task Details";
        }

        [RelayCommand]
        private void GoBack()
        {
            _navigationService.NavigateTo<MyTasksViewModel>();
        }

        [RelayCommand]
        private async Task SaveChanges()
        {
            IsBusy = true;
            
            // Sync values to the task model
            Task.Status = Status;
            Task.PercentComplete = (int)PercentComplete;
            Task.IsOnHold = IsOnHold;
            Task.HoldReason = HoldReason;

            try
            {
                await _taskService.UpdateTaskAsync(Task);
                _navigationService.GoBack();
            }
            catch (Exception ex)
            {
                // We should handle the error, but for now just log
                System.Diagnostics.Debug.WriteLine($"ERROR SAVING TASK: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnStatusChanged(string value)
        {
            if (IsOnHold && value != "On Hold")
            {
                IsOnHold = false; // Turn off hold if they pick a different status
            }
 
            switch (value)
            {
                case "Not Started": PercentComplete = 0; break;
                case "Started": PercentComplete = 25; break;
                case "Halfway": PercentComplete = 50; break;
                case "Almost Done": PercentComplete = 75; break;
                case "Completed": PercentComplete = 100; break;
                case "Done": PercentComplete = 100; break;
            }
        }
 
        partial void OnIsOnHoldChanged(bool value)
        {
            if (value)
            {
                Status = "On Hold";
            }
            else if (Status == "On Hold")
            {
                // Revert to status based on current progress
                if (PercentComplete >= 100) Status = "Completed";
                else if (PercentComplete >= 75) Status = "Almost Done";
                else if (PercentComplete >= 50) Status = "Halfway";
                else if (PercentComplete > 0) Status = "Started";
                else Status = "Not Started";
            }
        }
    }
}
