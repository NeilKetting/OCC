using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace OCC.Mobile.Features.Tasks
{
    public partial class RedesignTasksViewModel : ViewModelBase, IDisposable
    {
        #region Services
        private readonly INavigationService _navigationService;
        private readonly ISignalRService _signalRService;
        private readonly System.Threading.SemaphoreSlim _loadSemaphore = new(1, 1);
        private System.Threading.CancellationTokenSource? _cts;

        public IProjectTaskService TaskService { get; }
        public IProjectService ProjectService { get; }
        public IAuthService AuthService { get; }
        public ITaskCommentService CommentService { get; }
        #endregion

        #region Properties
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _showOverdueOnly;

        [ObservableProperty]
        private bool _showDueTodayOnly;

        [ObservableProperty]
        private bool _showDueThisWeekOnly;

        [ObservableProperty]
        private bool _showOnHoldOnly;

        [ObservableProperty]
        private bool _showFilters;

        [ObservableProperty]
        private bool _sortByDate;

        [ObservableProperty]
        private int _totalTasksCount;

        [ObservableProperty]
        private int _completedTasksCount;

        [ObservableProperty]
        private double _overallProgressPercentage;

        private Guid? _targetTaskId;
        public Guid? TargetTaskId
        {
            get => _targetTaskId;
            set
            {
                if (_targetTaskId != value)
                {
                    _targetTaskId = value;
                    if (value.HasValue)
                    {
                        LoadData().FireAndForget();
                    }
                }
            }
        }

        [ObservableProperty]
        private Guid? _projectId;

        partial void OnProjectIdChanged(Guid? value) => LoadData().FireAndForget();

        public ObservableCollection<ProjectGroupViewModel> ProjectGroups { get; } = new();
        #endregion

        #region Constructor & Lifecycle
        public RedesignTasksViewModel(
            INavigationService navigationService,
            IProjectTaskService taskService,
            IProjectService projectService,
            ISignalRService signalRService,
            IAuthService authService,
            ITaskCommentService commentService)
        {
            _navigationService = navigationService;
            TaskService = taskService;
            ProjectService = projectService;
            _signalRService = signalRService;
            AuthService = authService;
            CommentService = commentService;

            Title = "My Tasks";

            _signalRService.EntityUpdated += OnEntityUpdated;

            if (ProjectId == null)
            {
                LoadData().FireAndForget();
            }
        }

        private void OnEntityUpdated(string entityType, string action, Guid id)
        {
            if (entityType == "ProjectTask" || entityType == "Project")
            {
                LoadData().FireAndForget();
            }
        }

        public override void Dispose()
        {
            _signalRService.EntityUpdated -= OnEntityUpdated;
            _cts?.Cancel();
            _cts?.Dispose();
            base.Dispose();
        }
        #endregion

        #region Filter Callbacks
        partial void OnSearchTextChanged(string value) => ApplyFiltersAndRebuildHierarchy();
        partial void OnShowOverdueOnlyChanged(bool value) => ApplyFiltersAndRebuildHierarchy();
        partial void OnShowDueTodayOnlyChanged(bool value) => ApplyFiltersAndRebuildHierarchy();
        partial void OnShowDueThisWeekOnlyChanged(bool value) => ApplyFiltersAndRebuildHierarchy();
        partial void OnShowOnHoldOnlyChanged(bool value) => ApplyFiltersAndRebuildHierarchy();
        partial void OnSortByDateChanged(bool value) => ApplyFiltersAndRebuildHierarchy();
        #endregion

        #region Commands
        [RelayCommand]
        private void GoBack()
        {
            _navigationService.NavigateTo<Dashboard.DashboardViewModel>();
        }

        [RelayCommand]
        private void ToggleFilters()
        {
            ShowFilters = !ShowFilters;
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            ShowOverdueOnly = false;
            ShowDueTodayOnly = false;
            ShowDueThisWeekOnly = false;
            ShowOnHoldOnly = false;
            SortByDate = false;

            ApplyFiltersAndRebuildHierarchy();
        }

        [RelayCommand]
        public void ExpandAll()
        {
            foreach (var proj in ProjectGroups)
            {
                proj.IsExpanded = true;
                foreach (var sec in proj.Sections)
                {
                    sec.IsExpanded = true;
                }
            }
        }

        [RelayCommand]
        public void CollapseAll()
        {
            foreach (var proj in ProjectGroups)
            {
                proj.IsExpanded = false;
                foreach (var sec in proj.Sections)
                {
                    sec.IsExpanded = false;
                    foreach (var task in sec.Tasks)
                    {
                        task.IsExpanded = false;
                    }
                }
            }
        }

        private readonly object _tasksLock = new();
        private List<ProjectTask> _allTasks = new();
        private Dictionary<Guid, Project> _allProjects = new();

        [RelayCommand]
        public async Task LoadData()
        {
            _cts?.Cancel();
            _cts = new System.Threading.CancellationTokenSource();
            var token = _cts.Token;

            await _loadSemaphore.WaitAsync(token);
            try
            {
                if (token.IsCancellationRequested) return;

                IsBusy = true;

                // Update Title if ProjectId is set
                if (ProjectId.HasValue)
                {
                    var project = await ProjectService.GetProjectAsync(ProjectId.Value);
                    Title = project != null ? $"{project.Name} Tasks" : "Project Tasks";
                }
                else
                {
                    Title = "My Tasks";
                }

                // Load all assigned projects first
                var projects = await ProjectService.GetProjectsAsync(assignedToMe: true);
                if (token.IsCancellationRequested) return;
                lock (_tasksLock)
                {
                    _allProjects = projects.GroupBy(p => p.Id).Select(g => g.First()).ToDictionary(p => p.Id);
                }

                // Load first 15 assigned tasks
                var firstTasks = await TaskService.GetTasksAsync(projectId: ProjectId, assignedToMe: true, skip: 0, take: 15);
                if (token.IsCancellationRequested) return;
                lock (_tasksLock)
                {
                    _allTasks = firstTasks.ToList();
                }

                ApplyFiltersAndRebuildHierarchy();
                IsBusy = false;

                // Load remaining tasks in the background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var remainingTasks = await TaskService.GetTasksAsync(projectId: ProjectId, assignedToMe: true, skip: 15, take: 1500);
                        if (token.IsCancellationRequested) return;

                        lock (_tasksLock)
                        {
                            var existingIds = new HashSet<Guid>(_allTasks.Select(t => t.Id));
                            foreach (var t in remainingTasks)
                            {
                                if (!existingIds.Contains(t.Id))
                                {
                                    _allTasks.Add(t);
                                }
                            }
                        }
                        ApplyFiltersAndRebuildHierarchy();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading remaining tasks: {ex.Message}");
                    }
                }, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading redesign tasks: {ex.Message}");
                IsBusy = false;
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }
        #endregion

        #region Hierarchy Rebuilder
        private void ApplyFiltersAndRebuildHierarchy()
        {
            List<ProjectTask> tasksCopy;
            Dictionary<Guid, Project> projectsCopy;

            lock (_tasksLock)
            {
                tasksCopy = _allTasks.ToList();
                projectsCopy = _allProjects.ToDictionary(entry => entry.Key, entry => entry.Value);
            }

            bool hasTargetInTasks = TargetTaskId.HasValue && tasksCopy.Any(t => t.Id == TargetTaskId.Value);

            if (TargetTaskId.HasValue && hasTargetInTasks)
            {
                // Clear filters to guarantee the target task is visible in the loaded list
                SearchText = string.Empty;
                ShowOverdueOnly = false;
                ShowDueTodayOnly = false;
                ShowDueThisWeekOnly = false;
                ShowOnHoldOnly = false;
                SortByDate = false;
            }

            // Filter child tasks based on SearchText and chips
            var filteredTasks = tasksCopy.Where(t =>
            {
                if (t.IsGroup) return false;

                if (!string.IsNullOrWhiteSpace(SearchText) && !t.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (ShowOverdueOnly && !t.IsOverdue)
                    return false;

                if (ShowDueTodayOnly && t.FinishDate.Date != DateTime.Today)
                    return false;

                if (ShowDueThisWeekOnly)
                {
                    var nextWeek = DateTime.Today.AddDays(7);
                    if (t.FinishDate.Date < DateTime.Today || t.FinishDate.Date > nextWeek)
                        return false;
                }

                if (ShowOnHoldOnly && !t.IsOnHold)
                    return false;

                return true;
            }).ToList();

            // Group them by Project
            var newGroups = new List<ProjectGroupViewModel>();
            var tasksByProject = SortByDate
                ? filteredTasks.GroupBy(t => t.ProjectId).OrderBy(g => g.Min(t => t.FinishDate))
                : filteredTasks.GroupBy(t => t.ProjectId);

            foreach (var projectGroup in tasksByProject)
            {
                var projId = projectGroup.Key;
                if (!projId.HasValue) continue;

                projectsCopy.TryGetValue(projId.Value, out var project);
                var projName = project?.Name ?? "Unknown Project";
                var projStatus = project?.Status ?? "Started";

                var projVm = new ProjectGroupViewModel
                {
                    Id = projId.Value,
                    Title = projName,
                    Status = projStatus,
                    IsExpanded = (TargetTaskId.HasValue && hasTargetInTasks) ? false : true
                };

                // Identify sections (tasks in tasksCopy where IsGroup == true for this project)
                var sections = tasksCopy.Where(t => t.IsGroup && t.ProjectId == projId.Value).ToList();
                var tasksBySection = SortByDate
                    ? projectGroup.GroupBy(t => t.ParentId).OrderBy(g => g.Min(t => t.FinishDate))
                    : projectGroup.GroupBy(t => t.ParentId);

                foreach (var sectionGroup in tasksBySection)
                {
                    var parentId = sectionGroup.Key;
                    var sectionTask = sections.FirstOrDefault(s => s.Id == parentId);

                    var sectionVm = new SectionBlockViewModel
                    {
                        Id = parentId ?? Guid.Empty,
                        Title = sectionTask?.Name ?? "General Tasks",
                        Status = sectionTask?.Status ?? "In Progress",
                        IsExpanded = (TargetTaskId.HasValue && hasTargetInTasks) ? false : true
                    };

                    var sortedTasks = SortByDate 
                        ? sectionGroup.OrderBy(t => t.FinishDate).ToList()
                        : sectionGroup.OrderBy(t => t.OrderIndex).ToList();
                    for (int i = 0; i < sortedTasks.Count; i++)
                    {
                        var isTarget = TargetTaskId.HasValue && sortedTasks[i].Id == TargetTaskId.Value;
                        var taskRow = new RedesignTaskRowViewModel(sortedTasks[i], this)
                        {
                            IsLast = (i == sortedTasks.Count - 1),
                            ProjectTitle = projName,
                            IsExpanded = isTarget
                        };

                        if (isTarget)
                        {
                            projVm.IsExpanded = true;
                            sectionVm.IsExpanded = true;
                        }

                        sectionVm.Tasks.Add(taskRow);
                    }

                    projVm.Sections.Add(sectionVm);
                }

                // Calculate totals
                projVm.TotalTasks = projectGroup.Count();
                projVm.CompletedTasks = projectGroup.Count(t => t.IsComplete);

                newGroups.Add(projVm);
            }

            // Clear target ID after building hierarchy so subsequent updates don't keep expanding it
            if (TargetTaskId.HasValue && hasTargetInTasks)
            {
                TargetTaskId = null;
            }

            // Update collection and stats on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ProjectGroups.Clear();
                foreach (var g in newGroups)
                {
                    ProjectGroups.Add(g);
                }

                // Overall Stats
                var total = filteredTasks.Count;
                var completed = filteredTasks.Count(t => t.IsComplete);
                TotalTasksCount = total;
                CompletedTasksCount = completed;
                OverallProgressPercentage = total > 0 ? (double)completed / total * 100 : 0;
            });
        }
        #endregion
    }

    #region Nested Helper ViewModels
    public partial class ProjectGroupViewModel : ObservableObject
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        [ObservableProperty]
        private int _totalTasks;

        [ObservableProperty]
        private int _completedTasks;

        [ObservableProperty]
        private bool _isExpanded = true;

        public double ProgressPercentage => TotalTasks > 0 ? (double)CompletedTasks / TotalTasks * 100 : 0;
        public double ProgressAngle => ProgressPercentage * 3.6;

        public ObservableCollection<SectionBlockViewModel> Sections { get; } = new();

        [RelayCommand]
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }

    public partial class SectionBlockViewModel : ObservableObject
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isExpanded = true;

        public int TotalTasks => Tasks.Count;

        public ObservableCollection<RedesignTaskRowViewModel> Tasks { get; } = new();

        [RelayCommand]
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }

    public partial class RedesignTaskRowViewModel : ObservableObject
    {
        private readonly RedesignTasksViewModel _parent;
        public ProjectTask Task { get; }

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private string _editStatus = string.Empty;

        [ObservableProperty]
        private int _editProgress;

        [ObservableProperty]
        private string _editComment = string.Empty;

        [ObservableProperty]
        private bool _isSaving;

        [ObservableProperty]
        private bool _isSaved;

        [ObservableProperty]
        private string _editHoldReason = string.Empty;

        public bool ShowStandardSaveState => !IsSaving && !IsSaved;

        public bool CanSave => !IsSaving && (EditStatus != "On Hold" || !string.IsNullOrWhiteSpace(EditHoldReason));

        public bool IsOnHoldVisible => EditStatus != "Completed";

        partial void OnIsSavingChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowStandardSaveState));
            OnPropertyChanged(nameof(CanSave));
            SaveUpdateCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsSavedChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowStandardSaveState));
        }

        partial void OnEditHoldReasonChanged(string value)
        {
            OnPropertyChanged(nameof(CanSave));
            SaveUpdateCommand.NotifyCanExecuteChanged();
        }

        public ObservableCollection<AttachedPhotoViewModel> AttachedPhotos { get; } = new();

        [ObservableProperty]
        private bool _isLast;

        [ObservableProperty]
        private string _projectTitle = string.Empty;

        public string DisplayStatus
        {
            get
            {
                if (Task.IsOverdue && EditStatus != "Completed") return "Overdue";
                return EditStatus;
            }
        }

        public RedesignTaskRowViewModel(ProjectTask task, RedesignTasksViewModel parent)
        {
            Task = task;
            _parent = parent;
            ResetEditState();
        }

        public void ResetEditState()
        {
            EditStatus = Task.IsOnHold ? "On Hold" : Task.Status;
            EditProgress = Task.PercentComplete;
            EditComment = string.Empty;
            EditHoldReason = Task.HoldReason ?? string.Empty;
            AttachedPhotos.Clear();
            IsSaving = false;
            IsSaved = false;
        }

        partial void OnEditStatusChanged(string value)
        {
            OnPropertyChanged(nameof(DisplayStatus));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(IsOnHoldVisible));
            SaveUpdateCommand.NotifyCanExecuteChanged();

            int targetProgress = value switch
            {
                "Not Started" => 0,
                "Started" => 15,
                "Halfway" => 50,
                "Almost Done" => 85,
                "Completed" => 100,
                "On Hold" => EditProgress,
                _ => EditProgress
            };

            AnimateProgressTo(targetProgress).FireAndForget();
        }

        private async Task AnimateProgressTo(int target)
        {
            int start = EditProgress;
            if (start == target) return;

            int steps = 12;
            int delay = 16; // 16ms per frame (~60 FPS)

            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                double ease = 1 - Math.Pow(1 - t, 3); // Cubic ease-out
                EditProgress = (int)(start + (target - start) * ease);
                await System.Threading.Tasks.Task.Delay(delay);
            }

            EditProgress = target;
        }

        [RelayCommand]
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
            if (!IsExpanded)
            {
                ResetEditState();
            }
        }

        [RelayCommand]
        private void SetStatus(string status)
        {
            EditStatus = status;
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveUpdate()
        {
            if (IsSaving) return;
            IsSaving = true;

            try
            {
                if (EditStatus == "On Hold")
                {
                    Task.IsOnHold = true;
                    Task.Status = "On Hold";
                    Task.HoldReason = EditHoldReason;
                    Task.PercentComplete = EditProgress;
                }
                else
                {
                    Task.IsOnHold = false;
                    Task.HoldReason = string.Empty;
                    Task.PercentComplete = EditProgress;
                    if (EditProgress >= 100)
                    {
                        Task.Status = "Completed";
                    }
                    else
                    {
                        Task.Status = EditStatus;
                    }
                }

                // Call core update
                await _parent.TaskService.UpdateTaskAsync(Task);

                // Add comment if any
                var currentUser = _parent.AuthService.CurrentUser?.DisplayName ?? "Site Manager";
                if (!string.IsNullOrWhiteSpace(EditComment))
                {
                    var comment = new TaskComment
                    {
                        Id = Guid.NewGuid(),
                        TaskId = Task.Id,
                        Content = EditComment,
                        CreatedAtUtc = DateTime.UtcNow,
                        AuthorName = currentUser
                    };
                    await _parent.CommentService.AddCommentAsync(comment);
                }

                // Upload all attached photos and save their individual explanations as comments
                foreach (var photo in AttachedPhotos)
                {
                    try
                    {
                        await _parent.TaskService.UploadAttachmentAsync(Task.Id, photo.FilePath, currentUser);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error uploading file {photo.FilePath}: {ex.Message}");
                    }

                    if (!string.IsNullOrWhiteSpace(photo.Description))
                    {
                        var photoComment = new TaskComment
                        {
                            Id = Guid.NewGuid(),
                            TaskId = Task.Id,
                            Content = $"📸 [Photo: {photo.FileName}] {photo.Description}",
                            CreatedAtUtc = DateTime.UtcNow,
                            AuthorName = currentUser
                        };
                        await _parent.CommentService.AddCommentAsync(photoComment);
                    }
                }

                IsSaved = true;
                await System.Threading.Tasks.Task.Delay(900); // Visual feedback pause

                IsExpanded = false;
                ResetEditState();

                // Reload layout stats
                await _parent.LoadData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating task: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
            }
        }

        [RelayCommand]
        private async Task AttachPhoto(object parameter)
        {
            if (parameter is not Visual visual) return;
            var topLevel = TopLevel.GetTopLevel(visual);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Attach Task Photo",
                AllowMultiple = true,
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            });

            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    AttachedPhotos.Add(new AttachedPhotoViewModel
                    {
                        FilePath = file.Path.LocalPath
                    });
                }
            }
        }

        [RelayCommand]
        private void RemovePhoto(AttachedPhotoViewModel photo)
        {
            if (photo != null)
            {
                AttachedPhotos.Remove(photo);
            }
        }
    }

    public partial class AttachedPhotoViewModel : ObservableObject
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => System.IO.Path.GetFileName(FilePath);

        [ObservableProperty]
        private string _description = string.Empty;
    }
    #endregion
}
