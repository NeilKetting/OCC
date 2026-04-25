using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Messages;

namespace OCC.Client.Mobile.Features.Dashboard
{
    public partial class SiteManagerDashboardViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IAuthService _authService;
        private readonly IEmployeeService _employeeService;
        private readonly IProjectTaskRepository _taskRepository;
        private readonly IRepository<TaskComment> _commentRepository;
        private readonly ITaskAttachmentService _attachmentService;

        [ObservableProperty]
        private ObservableCollection<DashboardProjectViewModel> _projects = new();

        [ObservableProperty]
        private ObservableCollection<DashboardTaskViewModel> _overdueTasks = new();

        [ObservableProperty]
        private ObservableCollection<DashboardTaskViewModel> _todayTasks = new();

        [ObservableProperty]
        private ObservableCollection<DashboardTaskViewModel> _onHoldTasks = new();

        [ObservableProperty]
        private bool _hasProjects;

        [ObservableProperty]
        private string _diagnosticInfo = "Initializing...";

        public SiteManagerDashboardViewModel(
            IProjectService projectService,
            IAuthService authService,
            IEmployeeService employeeService,
            IProjectTaskRepository taskRepository,
            IRepository<TaskComment> commentRepository,
            ITaskAttachmentService attachmentService)
        {
            _projectService = projectService;
            _authService = authService;
            _employeeService = employeeService;
            _taskRepository = taskRepository;
            _commentRepository = commentRepository;
            _attachmentService = attachmentService;
            Title = "Site Manager Dashboard";

            // Register for real-time updates
            WeakReferenceMessenger.Default.Register<EntityUpdatedMessage>(this, async (r, m) =>
            {
                if (m.Value.EntityType == "ProjectTask" || m.Value.EntityType == "Project")
                {
                    await LoadProjectsAsync();
                }
            });
        }

        public async Task LoadProjectsAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                
                // 1. Get current logged in user
                var user = _authService.CurrentUser;
                if (user == null) return;

                // 2. Resolve Employee record
                var employees = await _employeeService.GetEmployeesAsync();
                var currentEmployee = employees.FirstOrDefault(e => e.LinkedUserId == user.Id);
                
                if (currentEmployee == null)
                {
                    currentEmployee = employees.FirstOrDefault(e => e.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
                }

                if (currentEmployee == null)
                {
                    Projects.Clear();
                    HasProjects = false;
                    return;
                }

                // 3. Fetch Projects & Tasks
                var allProjects = await _projectService.GetProjectSummariesAsync();
                var allMyTasks = await _taskRepository.GetMyTasksAsync();
                
                // 4. Filter by Site Manager ID (with Name fallback for robustness)
                var filteredProjects = allProjects
                    .Where(p => p.SiteManagerId == currentEmployee.Id || 
                               (p.SiteManagerName != null && p.SiteManagerName.Equals(currentEmployee.DisplayName, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(p => p.StartDate)
                    .ToList();

                Projects.Clear();
                OverdueTasks.Clear();
                TodayTasks.Clear();
                OnHoldTasks.Clear();

                foreach (var project in filteredProjects)
                {
                    var projectVm = new DashboardProjectViewModel(project, new List<ProjectTask>(), _taskRepository, _commentRepository, _attachmentService);
                    Projects.Add(projectVm);
                }

                // Organize all tasks into categories
                foreach (var task in allMyTasks)
                {
                    var taskVm = new DashboardTaskViewModel(task, _taskRepository, _commentRepository, _attachmentService);
                    
                    if (task.IsOnHold)
                    {
                        OnHoldTasks.Add(taskVm);
                    }
                    else if (task.IsOverdue)
                    {
                        OverdueTasks.Add(taskVm);
                    }
                    else if (task.FinishDate.Date == DateTime.Today.Date && !task.IsComplete)
                    {
                        TodayTasks.Add(taskVm);
                    }
                }

                HasProjects = Projects.Any();
                
                if (!HasProjects)
                {
                    var firstProject = allProjects.FirstOrDefault();
                    DiagnosticInfo = $"Logged in as: {user.Email}\n" +
                                     $"Employee ID: {currentEmployee?.Id.ToString() ?? "N/A"}\n" +
                                     $"Total Projects found: {allProjects.Count()}\n" +
                                     $"First Project SM ID: {firstProject?.SiteManagerId.ToString() ?? "NULL"}";
                }
                else
                {
                    DiagnosticInfo = string.Empty;
                }
            }
            catch (Exception)
            {
                // TODO: Log error
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            var count = await _projectService.SyncAssignmentsAsync();
            if (count > 0)
            {
                DiagnosticInfo += $"\nSync Result: {count} projects recovered.";
            }
            await LoadProjectsAsync();
        }

        [RelayCommand]
        private void NavigateToProject(ProjectSummaryDto project)
        {
            WeakReferenceMessenger.Default.Send(new OpenProjectMessage(project.Id));
        }
    }

    public class OpenProjectMessage
    {
        public Guid ProjectId { get; }
        public OpenProjectMessage(Guid projectId) => ProjectId = projectId;
    }

    /// <summary>
    /// ViewModel for an individual project card on the dashboard, including its daily tasks.
    /// </summary>
    public partial class DashboardProjectViewModel : ObservableObject
    {
        private readonly IProjectTaskRepository _taskRepository;
        private readonly IRepository<TaskComment> _commentRepository;
        private readonly ITaskAttachmentService _attachmentService;

        public ProjectSummaryDto Project { get; }
        public ObservableCollection<DashboardTaskViewModel> DailyTasks { get; } = new();

        [ObservableProperty]
        private bool _hasDailyTasks;

        public DashboardProjectViewModel(
            ProjectSummaryDto project, 
            IEnumerable<ProjectTask> dailyTasks,
            IProjectTaskRepository taskRepository,
            IRepository<TaskComment> commentRepository,
            ITaskAttachmentService attachmentService)
        {
            Project = project;
            _taskRepository = taskRepository;
            _commentRepository = commentRepository;
            _attachmentService = attachmentService;

            foreach (var task in dailyTasks)
            {
                DailyTasks.Add(new DashboardTaskViewModel(task, _taskRepository, _commentRepository, _attachmentService));
            }
            HasDailyTasks = DailyTasks.Any();
        }
    }

    /// <summary>
    /// ViewModel for an individual task item with inline interaction logic.
    /// </summary>
    public partial class DashboardTaskViewModel : ObservableObject
    {
        private readonly IProjectTaskRepository _taskRepository;
        private readonly IRepository<TaskComment> _commentRepository;
        private readonly ITaskAttachmentService _attachmentService;

        public ProjectTask Task { get; }

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private string _newCommentText = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public DashboardTaskViewModel(
            ProjectTask task,
            IProjectTaskRepository taskRepository,
            IRepository<TaskComment> commentRepository,
            ITaskAttachmentService attachmentService)
        {
            Task = task;
            _taskRepository = taskRepository;
            _commentRepository = commentRepository;
            _attachmentService = attachmentService;
        }

        [RelayCommand]
        private void ToggleExpand() => IsExpanded = !IsExpanded;

        [RelayCommand]
        private async Task ToggleStatusAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                Task.IsComplete = !Task.IsComplete;
                await _taskRepository.UpdateAsync(Task);
                OnPropertyChanged(nameof(Task));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task AddCommentAsync()
        {
            if (string.IsNullOrWhiteSpace(NewCommentText) || IsBusy) return;

            try
            {
                IsBusy = true;
                var comment = new TaskComment
                {
                    TaskId = Task.Id,
                    Content = NewCommentText,
                    CreatedAtUtc = DateTime.UtcNow,
                    AuthorName = "Site Manager" // In reality, fetch from AuthService
                };

                await _commentRepository.AddAsync(comment);
                Task.Comments.Add(comment);
                NewCommentText = string.Empty;
                OnPropertyChanged(nameof(Task));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task AttachPhotoAsync()
        {
            // Implementation would use Avalonia's StorageProvider
            // For now, we'll simulate the call or mark the intention
            // TODO: Integrate with actual Camera service
        }
    }
}
