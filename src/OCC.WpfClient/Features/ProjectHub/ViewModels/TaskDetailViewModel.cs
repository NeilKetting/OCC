using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.DTOs;
using OCC.Shared.Enums;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.ModelWrappers;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.ProjectHub.Models;
using System.Collections.ObjectModel;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class TaskDetailViewModel : ViewModelBase
    {
        private readonly IProjectTaskService _projectTaskService;
        private readonly IEmployeeService _employeeService;
        private readonly IUserService _userService;
        private readonly IProjectService _projectService;
        private readonly ITaskAssignmentService _assignmentService;
        private readonly ITaskCommentService _commentService;
        private readonly ITaskAttachmentService _attachmentService;
        private readonly ISubContractorService _subContractorService;
        private readonly IDialogService _dialogService;
        private readonly IAuthService _authService;
        private readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1);
        private bool _isSuppressingUpdates = false;
        private Guid _currentTaskId;

        [ObservableProperty] private ProjectTaskWrapper _task;
        [ObservableProperty] private string _newCommentContent = string.Empty;
        [ObservableProperty] private string _newToDoContent = string.Empty;
        [ObservableProperty] private bool _isShowingAllSubtasks;
        [ObservableProperty] private bool _isCreateMode;
        [ObservableProperty] private string _parentTaskName = string.Empty;
        [ObservableProperty] private Project? _selectedProject;
        
        public bool IsProjectLinkVisible => IsCreateMode;
        public bool IsSubtask => Task?.Model?.ParentId != null;
        public bool IsParentTask => !IsSubtask;
        public bool IsManualProgressEnabled => Task != null && !Task.HasSubtasks;
        public event EventHandler? CloseInitiated;
        public event EventHandler? CloseFinished;
        public int CommentsCount => Comments.Count;
        public int SubtaskCount => Subtasks.Count;
        public int AttachmentsCount => Attachments.Count;

        public ObservableCollection<Project> AvailableProjects { get; } = new();
        public ObservableCollection<TaskComment> Comments { get; } = new();
        public ObservableCollection<TaskAttachment> Attachments { get; } = new();
        public ObservableCollection<ProjectTask> Subtasks { get; } = new();
        public ObservableCollection<ProjectTask> VisibleSubtasks { get; } = new();
        public ObservableCollection<TaskAssignment> Assignments { get; } = new();
        public ObservableCollection<EmployeeSummaryDto> AvailableStaff { get; } = new();
        public ObservableCollection<User> AvailableContractors { get; } = new();
        public ObservableCollection<ToDoItemWrapper> ToDoList { get; } = new();
        public ObservableCollection<ReminderFrequency> ReminderFrequencies { get; } = new(Enum.GetValues<ReminderFrequency>());
        public List<string> AvailableStatuses { get; } = new() { "Not Started", "Started", "Halfway", "Almost Done", "Completed" };

        [ObservableProperty] private string _assigneeSearchText = string.Empty;
        public ObservableCollection<AssigneeSelectionViewModel> AllPotentialAssignees { get; } = new();
        public ObservableCollection<AssigneeSelectionViewModel> FilteredAssignees { get; } = new();

        partial void OnAssigneeSearchTextChanged(string value)
        {
            ApplyAssigneeFilter();
        }

        private void ApplyAssigneeFilter()
        {
            FilteredAssignees.Clear();
            var search = AssigneeSearchText?.ToLowerInvariant();
            
            var items = string.IsNullOrWhiteSpace(search) 
                ? AllPotentialAssignees 
                : AllPotentialAssignees.Where(a => 
                    a.Name.ToLowerInvariant().Contains(search) || 
                    a.Role.ToLowerInvariant().Contains(search) ||
                    a.Branch.ToLowerInvariant().Contains(search));

            foreach (var item in items) FilteredAssignees.Add(item);
        }

        public TaskDetailViewModel(
            IProjectTaskService projectTaskService,
            IEmployeeService employeeService,
            IUserService userService,
            IProjectService projectService,
            ITaskAssignmentService assignmentService,
            ITaskCommentService commentService,
            ITaskAttachmentService attachmentService,
            ISubContractorService subContractorService,
            IDialogService dialogService,
            IAuthService authService)
        {
            _projectTaskService = projectTaskService;
            _employeeService = employeeService;
            _userService = userService;
            _projectService = projectService;
            _assignmentService = assignmentService;
            _commentService = commentService;
            _attachmentService = attachmentService;
            _subContractorService = subContractorService;
            _dialogService = dialogService;
            _authService = authService;
            
            _task = new ProjectTaskWrapper(new ProjectTask());
        }

        public async Task LoadTaskById(Guid taskId)
        {
            try 
            {
                UpdateStatus("Loading task details...");
                BusyText = "Loading task details...";
                IsBusy = true;
                
                var task = await _projectTaskService.GetTaskAsync(taskId);
                if (task != null) await LoadTaskModel(task);
                UpdateStatus("Ready");
            }
            catch (Exception ex)
            {
                NotifyError("Error", $"Error loading task: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        public async Task InitializeForCreation(Guid? projectId = null, Guid? parentTaskId = null)
        {
            IsBusy = true;
            IsCreateMode = true;
            
            if (parentTaskId.HasValue)
            {
                var parent = await _projectTaskService.GetTaskAsync(parentTaskId.Value);
                ParentTaskName = parent?.Name ?? "Parent Task";
            }

            var model = new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ParentId = parentTaskId,
                Status = "Not Started",
                Priority = "Medium",
                StartDate = DateTime.UtcNow,
                FinishDate = DateTime.UtcNow.AddDays(1)
            };
            LoadTask(model);
            _currentTaskId = model.Id;
            await LoadAssignableResources();
            IsBusy = false;
        }

        private async Task LoadTaskModel(ProjectTask task)
        {
            _isSuppressingUpdates = true;
            _currentTaskId = task.Id;
            LoadTask(task);
            await LoadAssignableResources();
            _isSuppressingUpdates = false;
        }

        private async Task LoadAssignableResources()
        {
            var staff = await _employeeService.GetEmployeesAsync();
            AvailableStaff.Clear();
            foreach (var s in staff.Where(e => e.Status == EmployeeStatus.Active)) AvailableStaff.Add(s);

            var users = await _userService.GetUsersAsync();
            AvailableContractors.Clear();
            foreach (var u in users.Where(user => user.UserRole == UserRole.ExternalContractor)) AvailableContractors.Add(u);

            var projects = await _projectService.GetProjectsAsync();
            AvailableProjects.Clear();
            foreach (var p in projects) AvailableProjects.Add(p);

            await LoadComments();
            await LoadAssignments();
            await LoadPotentialAssigneesAsync();
        }

        private async Task LoadPotentialAssigneesAsync()
        {
            AllPotentialAssignees.Clear();

            // 1. Add "Us" Options
            AllPotentialAssignees.Add(new AssigneeSelectionViewModel 
            { 
                Id = Guid.Empty, // Placeholder for company
                Name = "Circle Construction - Jhb", 
                Role = "Internal", 
                Type = AssigneeType.Staff,
                Branch = "Jhb"
            });
            AllPotentialAssignees.Add(new AssigneeSelectionViewModel 
            { 
                Id = Guid.Empty, 
                Name = "Orange Circle Construction - CPT", 
                Role = "Internal", 
                Type = AssigneeType.Staff,
                Branch = "CPT"
            });

            // 2. Load Employees
            var employees = await _employeeService.GetEmployeesAsync();
            foreach (var e in employees.Where(x => x.Status == EmployeeStatus.Active))
            {
                // Only Site Managers and Foremans as requested
                if (e.Role.ToString().Contains("Manager") || e.Role.ToString().Contains("Foreman"))
                {
                    AllPotentialAssignees.Add(new AssigneeSelectionViewModel
                    {
                        Id = e.Id,
                        Name = e.DisplayName,
                        Role = e.Role.ToString(),
                        Type = AssigneeType.Staff,
                        Branch = e.Branch
                    });
                }
            }

            // 3. Load SubContractors
            var subContractors = await _subContractorService.GetSubContractorSummariesAsync();
            foreach (var sc in subContractors)
            {
                AllPotentialAssignees.Add(new AssigneeSelectionViewModel
                {
                    Id = sc.Id,
                    Name = sc.Name,
                    Role = "Contractor",
                    Type = AssigneeType.Contractor,
                    Branch = sc.Branch
                });
            }

            // Mark existing assignments
            foreach (var assignee in AllPotentialAssignees)
            {
                assignee.IsSelected = Assignments.Any(a => a.AssigneeId == assignee.Id || (assignee.Id == Guid.Empty && a.AssigneeName == assignee.Name));
            }

            ApplyAssigneeFilter();
        }

        [RelayCommand]
        private async Task ToggleAssignment(AssigneeSelectionViewModel assignee)
        {
            if (assignee == null) return;

            // Toggle VM state
            assignee.IsSelected = !assignee.IsSelected;

            if (assignee.IsSelected)
            {
                var newAssignment = new TaskAssignment
                {
                    Id = Guid.NewGuid(),
                    TaskId = _currentTaskId,
                    AssigneeId = assignee.Id,
                    AssigneeName = assignee.Name,
                    AssigneeType = assignee.Type
                };

                Assignments.Add(newAssignment);
                if (!IsCreateMode) await _assignmentService.AddAssignmentAsync(newAssignment);
            }
            else
            {
                var existing = Assignments.FirstOrDefault(a => a.AssigneeId == assignee.Id || (assignee.Id == Guid.Empty && a.AssigneeName == assignee.Name));
                if (existing != null)
                {
                    Assignments.Remove(existing);
                    if (!IsCreateMode) await _assignmentService.DeleteAssignmentAsync(existing.Id);
                }
            }
            
            OnPropertyChanged(nameof(Task)); // Refresh initials etc
            Task.RefreshAssigneeInfo();
        }

        private async Task LoadComments()
        {
            Comments.Clear();
            var comments = await _commentService.GetCommentsAsync(_currentTaskId);
            foreach (var c in comments.OrderByDescending(x => x.CreatedAtUtc)) Comments.Add(c);
            OnPropertyChanged(nameof(CommentsCount));
        }

        private async Task LoadAssignments()
        {
            Assignments.Clear();
            var assignments = await _assignmentService.GetAssignmentsAsync(_currentTaskId);
            foreach (var a in assignments) Assignments.Add(a);
        }

        private void LoadTask(ProjectTask task)
        {
            if (Task != null) Task.PropertyChanged -= Task_PropertyChanged;
            Task = new ProjectTaskWrapper(task);
            Task.PropertyChanged += Task_PropertyChanged;
        }

        private async void Task_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isSuppressingUpdates || IsCreateMode) return;
            await UpdateTask();
        }

        private async Task UpdateTask()
        {
            await _updateLock.WaitAsync();
            try
            {
                UpdateStatus("Saving changes...");
                Task.CommitToModel();
                await _projectTaskService.UpdateTaskAsync(Task.Model);
                WeakReferenceMessenger.Default.Send(new TaskUpdatedMessage(_currentTaskId));
                UpdateStatus("Ready");
            }
            finally { _updateLock.Release(); }
        }

        [RelayCommand]
        private async Task AddComment()
        {
            if (string.IsNullOrWhiteSpace(NewCommentContent)) return;
            var user = _authService.CurrentUser;
            var comment = new TaskComment
            {
                TaskId = _currentTaskId,
                Content = NewCommentContent,
                AuthorName = $"{user?.FirstName} {user?.LastName}",
                AuthorEmail = user?.Email ?? "Unknown",
                CreatedAtUtc = DateTime.UtcNow
            };
            await _commentService.AddCommentAsync(comment);
            Comments.Insert(0, comment);
            NewCommentContent = string.Empty;
            OnPropertyChanged(nameof(CommentsCount));
            NotifySuccess("Comment Added", "Your comment has been posted.");
        }

        [RelayCommand]
        public void RequestClose()
        {
            CloseInitiated?.Invoke(this, EventArgs.Empty);
        }

        public void ConfirmClose()
        {
            CloseFinished?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void SetStatus(string status)
        {
            if (Task != null)
            {
                Task.Status = status;
            }
        }

        [RelayCommand]
        private void ToggleOnHold()
        {
            if (Task != null)
            {
                Task.IsOnHold = !Task.IsOnHold;
            }
        }

        [RelayCommand]
        private async Task CreateTask()
        {
            if (string.IsNullOrWhiteSpace(Task.Name)) return;
            Task.CommitToModel();
            Task.Model.Type = TaskType.Task;
            if (SelectedProject != null) Task.Model.ProjectId = SelectedProject.Id;
            await _projectTaskService.CreateTaskAsync(Task.Model);
            NotifySuccess("Task Created", $"Task '{Task.Name}' has been created successfully.");
            WeakReferenceMessenger.Default.Send(new TaskUpdatedMessage(Task.Model.Id));
            RequestClose();
        }
    }
}
