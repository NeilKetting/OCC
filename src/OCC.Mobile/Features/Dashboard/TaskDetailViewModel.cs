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
        private readonly ITaskCommentService _commentService;
        private readonly IAuthService _authService;
        private readonly ISignalRService _signalRService;
 
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
 
        [ObservableProperty]
        private string _newCommentContent;
 
        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<TaskComment> _comments = new();




        public TaskDetailViewModel(INavigationService navigationService, IProjectTaskService taskService, ITaskCommentService commentService, IAuthService authService, ISignalRService signalRService)
        {
            _navigationService = navigationService;
            _taskService = taskService;
            _commentService = commentService;
            _authService = authService;
            _signalRService = signalRService;
            
            _signalRService.EntityUpdated += OnEntityUpdated;
            
            // Default empty task
            _task = new ProjectTask { Name = "Task Detail" };
            _status = "Not Started";
            _holdReason = string.Empty;
            _newCommentContent = string.Empty;
            Title = "Task Details";
        }
 
        private void OnEntityUpdated(string entityType, string action, Guid id)
        {
            if (entityType == "TaskComment" && action == "Create")
            {
                LoadComments().FireAndForget();
            }
            else if (entityType == "ProjectTask" && id == Task?.Id)
            {
                // Refresh comments and task data if this specific task was updated
                LoadComments().FireAndForget();
                
                RefreshTaskData(id).FireAndForget();
            }
        }
 
        private async Task RefreshTaskData(Guid id)
        {
            var updatedTask = await _taskService.GetTaskAsync(id);
            if (updatedTask != null)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Task = updatedTask);
            }
        }
 
        public override void Dispose()
        {
            _signalRService.EntityUpdated -= OnEntityUpdated;
            base.Dispose();
        }

        partial void OnTaskChanged(ProjectTask value)
        {
            if (value != null)
            {
                Status = value.Status;
                PercentComplete = value.PercentComplete;
                IsOnHold = value.IsOnHold;
                HoldReason = value.HoldReason;
                
                // Explicitly notify to ensure UI bindings catch the initial state
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(PercentComplete));
                OnPropertyChanged(nameof(IsOnHold));
                OnPropertyChanged(nameof(HoldReason));
                
                LoadComments().FireAndForget();
            }
        }
 
        [RelayCommand]
        public async Task RefreshComments()
        {
            await LoadComments();
        }
 
        private async Task LoadComments()
        {
            if (Task?.Id == null || Task.Id == Guid.Empty) return;
            
            var comments = await _commentService.GetCommentsAsync(Task.Id);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
            {
                Comments.Clear();
                foreach (var c in comments.OrderByDescending(x => x.CreatedAtUtc)) Comments.Add(c);
            });
        }

        [RelayCommand]
        private void GoBack()
        {
            _navigationService.GoBack();
        }

        // SaveChanges removed as we now auto-update on status change

        partial void OnStatusChanged(string value)
        {
            if (value == "On Hold")
            {
                IsOnHold = true;
            }
            else if (IsOnHold)
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
                case "On Hold": IsOnHold = true; break;
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
        [RelayCommand]
        private async Task AddComment()
        {
            if (string.IsNullOrWhiteSpace(NewCommentContent)) return;
            
            var user = _authService.CurrentUser;
            var comment = new TaskComment
            {
                Id = Guid.NewGuid(),
                TaskId = Task.Id,
                Content = NewCommentContent,
                AuthorName = $"{user?.FirstName} {user?.LastName}",
                AuthorEmail = user?.Email ?? "Unknown",
                CreatedAtUtc = DateTime.UtcNow
            };
 
            try
            {
                await _commentService.AddCommentAsync(comment);
                Comments.Insert(0, comment);
                NewCommentContent = string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR ADDING COMMENT: {ex.Message}");
            }
        }
 
        [RelayCommand]
        private async Task SetStatus(string status)
        {
            if (Status == status) return;
            
            Status = status;
            
            // Auto-save on status change
            await UpdateTaskInternal();
        }
 
        private async Task UpdateTaskInternal()
        {
            if (IsBusy) return;
            IsBusy = true;
            
            try
            {
                // Sync values to the task model
                Task.Status = Status;
                Task.PercentComplete = (int)PercentComplete;
                Task.IsOnHold = IsOnHold;
                Task.HoldReason = HoldReason;
 
                await _taskService.UpdateTaskAsync(Task);
                
                // Show a brief success indicator or toast if we had one
                System.Diagnostics.Debug.WriteLine($"TASK UPDATED AUTOMATICALLY: {Status}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR AUTO-UPDATING TASK: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
