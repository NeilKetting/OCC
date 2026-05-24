using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
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
    public partial class ProjectTaskListViewModel : ViewModelBase, IOverlayProvider, IRecipient<TaskUpdatedMessage>, IRecipient<CreateTaskFromVariationOrderMessage>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IProjectTaskService _taskService;
        private readonly ISubContractorService _subContractorService;
        private readonly ITaskAssignmentService _assignmentService;
        private readonly IDialogService _dialogService;
        private readonly LocalSettingsService _settingsService;
        
        /// <summary> Global map of subcontractor names to their assigned hex colors for UI badges. </summary>
        public static Dictionary<string, string> SubContractorColorMap { get; } = new(StringComparer.OrdinalIgnoreCase);


        // Column Visibility
        [ObservableProperty] private bool _isStartVisible = true;
        [ObservableProperty] private bool _isEndVisible = true;
        [ObservableProperty] private bool _isProgressVisible = true;
        [ObservableProperty] private bool _isStageVisible = true;
        [ObservableProperty] private bool _isDurationVisible = true;
        [ObservableProperty] private bool _isAssignedVisible = true;
        [ObservableProperty] private bool _isPriorityVisible = true;

        [ObservableProperty] private bool _isColumnPickerOpen;

        [ObservableProperty] private ObservableCollection<ProjectTask> _tasks = new();
        [ObservableProperty] private ProjectTask? _selectedTask;
        [ObservableProperty] private bool _hasTasks;
        [ObservableProperty] private TaskDetailViewModel? _currentTaskDetail;
        [ObservableProperty] private Guid _projectId;
        [ObservableProperty] private string _parentTaskName = string.Empty;
        [ObservableProperty] private int _totalActionableTaskCount;

        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private string _selectedStageFilter = "All Stages";

        // Assignment Popup
        [ObservableProperty] private bool _isAssignPopupOpen;
        [ObservableProperty] private ProjectTask? _taskToAssign;
        [ObservableProperty] private ObservableCollection<SubContractorSelectionViewModel> _availableSubContractors = new();
        [ObservableProperty] private string _subContractorSearchQuery = string.Empty;
        [ObservableProperty] private string _selectedSpecialtyFilter = "All Specialties";
        [ObservableProperty] private ObservableCollection<string> _availableSpecialties = new() { "All Specialties" };

        // Smart Filters
        [ObservableProperty] private int _overdueCount;
        [ObservableProperty] private int _dueTodayCount;
        [ObservableProperty] private int _dueThisWeekCount;
        [ObservableProperty] private int _dueThisMonthCount;
        [ObservableProperty] private bool _isFilterPopupOpen;
        
        [ObservableProperty] private string _activeSmartFilter = "All Tasks"; // "All", "Overdue", "Today", "Week", "Month"
        [ObservableProperty] private Guid? _filterSubContractorId;

        public ObservableCollection<SubContractorFilterDto> SubContractorFilters { get; } = new();

        public ObservableCollection<string> AvailableStages { get; } = new() 
        {
            "All Stages", "Not Started", "Started", "Halfway", "Almost Done", "Completed", "On Hold"
        };

        private List<ProjectTask> _rootTasks = new();

        public ViewModelBase? ActiveOverlay => CurrentTaskDetail;

        public override void Dispose()
        {
            base.Dispose();
            Tasks.Clear();
            _rootTasks.Clear();
        }


        public ProjectTaskListViewModel(
            IServiceProvider serviceProvider, 
            IProjectTaskService taskService,
            ISubContractorService subContractorService,
            ITaskAssignmentService assignmentService,
            IDialogService dialogService,
            LocalSettingsService settingsService)
        {
            _serviceProvider = serviceProvider;
            _taskService = taskService;
            _subContractorService = subContractorService;
            _assignmentService = assignmentService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            Title = "Tasks";
            
            LoadLayout();
            WeakReferenceMessenger.Default.Register<TaskUpdatedMessage>(this);
            WeakReferenceMessenger.Default.Register<CreateTaskFromVariationOrderMessage>(this);
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.ProjectTaskListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsStartVisible = layout.Columns.FirstOrDefault(c => c.Header == "Start")?.IsVisible ?? true;
                IsEndVisible = layout.Columns.FirstOrDefault(c => c.Header == "End")?.IsVisible ?? true;
                IsProgressVisible = layout.Columns.FirstOrDefault(c => c.Header == "Progress")?.IsVisible ?? true;
                IsStageVisible = layout.Columns.FirstOrDefault(c => c.Header == "Stage")?.IsVisible ?? true;
                IsDurationVisible = layout.Columns.FirstOrDefault(c => c.Header == "Duration")?.IsVisible ?? true;
                IsAssignedVisible = layout.Columns.FirstOrDefault(c => c.Header == "Assigned")?.IsVisible ?? true;
                IsPriorityVisible = layout.Columns.FirstOrDefault(c => c.Header == "Priority")?.IsVisible ?? true;
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
                    new() { Header = "Assigned", IsVisible = IsAssignedVisible },
                    new() { Header = "Priority", IsVisible = IsPriorityVisible }
                }
            };
            _settingsService.Settings.ProjectTaskListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsStartVisibleChanged(bool value) => SaveLayout();
        partial void OnIsEndVisibleChanged(bool value) => SaveLayout();
        partial void OnIsProgressVisibleChanged(bool value) => SaveLayout();
        partial void OnIsStageVisibleChanged(bool value) => SaveLayout();
        partial void OnIsDurationVisibleChanged(bool value) => SaveLayout();
        partial void OnIsAssignedVisibleChanged(bool value) => SaveLayout();
        partial void OnIsPriorityVisibleChanged(bool value) => SaveLayout();

        [RelayCommand]
        private void ToggleColumnPicker() => IsColumnPickerOpen = !IsColumnPickerOpen;

        public async Task UpdateTasksAsync(Guid projectId, IEnumerable<ProjectTask> tasks)
        {
            try
            {
                IsBusy = true;
                BusyText = "Processing tasks...";
                
                // Allow UI thread to show the spinner before we block it with heavy rendering
                await Task.Delay(10);

                ProjectId = projectId;
                var taskList = tasks.ToList();

                // Refresh color map for badges - Ensure this completes BEFORE we refresh the display list
                try
                {
                    var contractors = await _subContractorService.GetSubContractorsAsync();
                    foreach (var sc in contractors)
                    {
                        if (!string.IsNullOrEmpty(sc.ColorTheme))
                            SubContractorColorMap[sc.Name] = sc.ColorTheme;
                    }
                    
                    // Add/Force internals to Orange as requested
                    SubContractorColorMap["Orange Circle Construction JHB"] = "#FF9800";
                    SubContractorColorMap["Orange Circle Construction CPT"] = "#FF9800";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error fetching subcontractor colors: {ex.Message}");
                }

                CalculateSmartStats(taskList);

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
                
                // Calculate total actionable (non-group) tasks for the header badge
                TotalActionableTaskCount = taskList.Count(t => !t.IsGroup);
                
                RefreshDisplayList();
            }
            finally
            {
                IsBusy = false;
            }
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
            // If it's a group and we're filtering by subbie, we might want to hide it if no children match
            // But usually we show the hierarchy.
            
            bool matchesSearch = string.IsNullOrWhiteSpace(SearchQuery) || 
                task.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);

            bool matchesStage = SelectedStageFilter == "All Stages" || 
                task.Status.Equals(SelectedStageFilter, StringComparison.OrdinalIgnoreCase);

            bool matchesSmart = true;
            if (ActiveSmartFilter == "Overdue") matchesSmart = task.IsOverdue;
            else if (ActiveSmartFilter == "Due Today") matchesSmart = task.FinishDate.Date == DateTime.Today;
            else if (ActiveSmartFilter == "Due This Week") 
            {
                var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                var endOfWeek = startOfWeek.AddDays(7);
                matchesSmart = task.FinishDate.Date >= startOfWeek && task.FinishDate.Date < endOfWeek;
            }
            else if (ActiveSmartFilter == "Due This Month") matchesSmart = task.FinishDate.Month == DateTime.Today.Month && task.FinishDate.Year == DateTime.Today.Year;

            bool matchesSubbie = true;
            if (FilterSubContractorId.HasValue)
            {
                matchesSubbie = task.Assignments?.Any(a => a.AssigneeId == FilterSubContractorId.Value) ?? false;
            }

            // If this task doesn't match but a child does, we still show the parent
            bool anyChildMatches = task.Children?.Any(c => MatchesFilter(c)) ?? false;

            return (matchesSearch && matchesStage && matchesSmart && matchesSubbie) || anyChildMatches;
        }

        private void CalculateSmartStats(List<ProjectTask> allTasks)
        {
            var actionable = allTasks.Where(t => !t.IsGroup).ToList();
            
            OverdueCount = actionable.Count(t => t.IsOverdue);
            DueTodayCount = actionable.Count(t => t.FinishDate.Date == DateTime.Today);
            
            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7);
            DueThisWeekCount = actionable.Count(t => t.FinishDate.Date >= startOfWeek && t.FinishDate.Date < endOfWeek);
            
            DueThisMonthCount = actionable.Count(t => t.FinishDate.Month == DateTime.Today.Month && t.FinishDate.Year == DateTime.Today.Year);

            // Subcontractor stats
            var subbieStats = new Dictionary<Guid, (string Name, int Count)>();
            foreach(var task in actionable)
            {
                if (task.Assignments == null) continue;
                foreach(var assign in task.Assignments)
                {
                    if (subbieStats.ContainsKey(assign.AssigneeId))
                    {
                        var stats = subbieStats[assign.AssigneeId];
                        subbieStats[assign.AssigneeId] = (stats.Name, stats.Count + 1);
                    }
                    else
                    {
                        subbieStats[assign.AssigneeId] = (assign.AssigneeName, 1);
                    }
                }
            }

            App.Current.Dispatcher.Invoke(() =>
            {
                SubContractorFilters.Clear();
                foreach(var kvp in subbieStats.OrderBy(x => x.Value.Name))
                {
                    SubContractorFilters.Add(new SubContractorFilterDto { Id = kvp.Key, Name = kvp.Value.Name, TaskCount = kvp.Value.Count });
                }
            });
        }

        [RelayCommand]
        private void ApplySmartFilter(string filter)
        {
            ActiveSmartFilter = filter;
            IsFilterPopupOpen = false;
            RefreshDisplayList();
        }

        [RelayCommand]
        private void ApplySubContractorFilter(Guid? subbieId)
        {
            FilterSubContractorId = subbieId;
            IsFilterPopupOpen = false;
            RefreshDisplayList();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            ActiveSmartFilter = "All Tasks";
            FilterSubContractorId = null;
            SelectedStageFilter = "All Stages";
            SearchQuery = string.Empty;
            IsFilterPopupOpen = false;
            RefreshDisplayList();
        }

        [RelayCommand]
        private void ToggleFilterPopup() => IsFilterPopupOpen = !IsFilterPopupOpen;

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

        partial void OnSubContractorSearchQueryChanged(string value) => RefreshFilteredSubContractors();
        partial void OnSelectedSpecialtyFilterChanged(string value) => RefreshFilteredSubContractors();

        private void RefreshFilteredSubContractors()
        {
            // We'll use the IsVisible property on the SelectionViewModel for simplicity in the ListBox
            foreach (var sc in AvailableSubContractors)
            {
                bool matchesSearch = string.IsNullOrWhiteSpace(SubContractorSearchQuery) || 
                    sc.Name.Contains(SubContractorSearchQuery, StringComparison.OrdinalIgnoreCase);
                
                bool matchesSpecialty = SelectedSpecialtyFilter == "All Specialties" || 
                    (sc.Specialty != null && sc.Specialty.Contains(SelectedSpecialtyFilter, StringComparison.OrdinalIgnoreCase));

                sc.IsVisible = matchesSearch && matchesSpecialty;
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

            var confirm = await _dialogService.ShowConfirmationAsync(
                "Delete Task", 
                $"Are you sure you want to delete task '{task.Name}'?\n\nThis action cannot be undone.");

            if (!confirm) return;

            await _taskService.DeleteTaskAsync(task.Id);
            Tasks.Remove(task);
            HasTasks = Tasks.Any();
            NotifySuccess("Task Deleted", $"Task '{task.Name}' has been removed.");
        }

        [RelayCommand]
        private async Task ShowAssignTo(ProjectTask task)
        {
            if (task == null) return;
            TaskToAssign = task;
            
            var contractors = await _subContractorService.GetSubContractorsAsync();
            var assignments = await _assignmentService.GetAssignmentsAsync(task.Id);
            var assignedIds = assignments.Select(a => a.AssigneeId).ToHashSet();

            var list = new List<SubContractorSelectionViewModel>();
            
            // Add "Internal" Options (Synced with TaskDetailViewModel)
            var jhb = new SubContractorSelectionViewModel(Guid.Empty, "Orange Circle Construction JHB", "Internal", "#FF9800") { Type = AssigneeType.Staff };
            jhb.IsSelected = assignments.Any(a => a.AssigneeId == Guid.Empty && a.AssigneeName == jhb.Name);
            list.Add(jhb);

            var cpt = new SubContractorSelectionViewModel(Guid.Empty, "Orange Circle Construction CPT", "Internal", "#FF9800") { Type = AssigneeType.Staff };
            cpt.IsSelected = assignments.Any(a => a.AssigneeId == Guid.Empty && a.AssigneeName == cpt.Name);
            list.Add(cpt);

            var specialties = new HashSet<string> { "All Specialties", "Internal" };

            foreach (var sc in contractors)
            {
                var vm = new SubContractorSelectionViewModel(sc.Id, sc.Name, sc.Specialties ?? "General", sc.ColorTheme)
                {
                    IsSelected = assignedIds.Contains(sc.Id)
                };
                list.Add(vm);

                if (!string.IsNullOrEmpty(sc.Specialties))
                {
                    foreach (var s in sc.Specialties.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        specialties.Add(s.Trim());
                    }
                }
            }

            AvailableSubContractors = new ObservableCollection<SubContractorSelectionViewModel>(list.OrderBy(x => x.Name));
            AvailableSpecialties = new ObservableCollection<string>(specialties.OrderBy(s => s));
            
            SubContractorSearchQuery = string.Empty;
            SelectedSpecialtyFilter = "All Specialties";
            RefreshFilteredSubContractors();
            
            IsAssignPopupOpen = true;
        }

        [RelayCommand]
        private async Task SaveAssignments()
        {
            if (TaskToAssign == null) return;

            var currentAssignments = await _assignmentService.GetAssignmentsAsync(TaskToAssign.Id);
            var selectedSubbies = AvailableSubContractors.Where(x => x.IsSelected).ToList();

            // Remove assignments no longer selected
            foreach (var assignment in currentAssignments)
            {
                if (!selectedSubbies.Any(x => x.Id == assignment.AssigneeId && (x.Id != Guid.Empty || x.Name == assignment.AssigneeName)))
                {
                    await _assignmentService.DeleteAssignmentAsync(assignment.Id);
                }
            }

            // Add new assignments
            foreach (var subbie in selectedSubbies)
            {
                if (!currentAssignments.Any(x => x.AssigneeId == subbie.Id && (subbie.Id != Guid.Empty || x.AssigneeName == subbie.Name)))
                {
                    await _assignmentService.AddAssignmentAsync(new TaskAssignment
                    {
                        TaskId = TaskToAssign.Id,
                        AssigneeId = subbie.Id,
                        AssigneeName = subbie.Name,
                        AssigneeType = subbie.Type
                    });
                }
            }

            // Update legacy string
            TaskToAssign.AssignedTo = string.Join(", ", selectedSubbies.Select(x => x.Name));
            await _taskService.UpdateTaskAsync(TaskToAssign);
            
            TaskToAssign.NotifyPropertyChanged(nameof(ProjectTask.AssigneeInitials));
            TaskToAssign.NotifyPropertyChanged(nameof(ProjectTask.AssignedTo));

            IsAssignPopupOpen = false;
            NotifySuccess("Assignments Saved", $"Task assignments for '{TaskToAssign.Name}' updated.");
        }

        [RelayCommand]
        private void CloseAssignPopup() => IsAssignPopupOpen = false;

        public async void Receive(TaskUpdatedMessage message)
        {
            try
            {
                // Find the task locally and update it
                var updatedTask = await _taskService.GetTaskAsync(message.TaskId);
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    var existing = Tasks.FirstOrDefault(t => t.Id == message.TaskId);
                    
                    if (updatedTask == null)
                    {
                        // Task was likely deleted on the server
                        if (existing != null)
                        {
                            Tasks.Remove(existing);
                            HasTasks = Tasks.Any();
                        }
                        return;
                    }

                    if (existing != null)
                    {
                        // Update properties manually to trigger UI refresh on the object in the list
                        existing.Status = updatedTask.Status;
                        existing.PercentComplete = updatedTask.PercentComplete;
                        existing.IsOnHold = updatedTask.IsOnHold;
                        existing.HoldReason = updatedTask.HoldReason;
                        existing.Name = updatedTask.Name;
                        existing.IsExpanded = updatedTask.IsExpanded; // Preserve or update
                        existing.StartDate = updatedTask.StartDate;
                        existing.FinishDate = updatedTask.FinishDate;
                        
                        // Force refresh colors and labels
                        existing.NotifyPropertyChanged(nameof(existing.StatusColor));
                        existing.NotifyPropertyChanged(nameof(existing.IsComplete));
                        existing.NotifyPropertyChanged(nameof(existing.IsOverdue));
                        existing.NotifyPropertyChanged(nameof(existing.Duration));
                    }
                    else
                    {
                        // If it's a new task or we can't find it, we might need to rebuild hierarchy
                        // But for simple updates, this is enough.
                    }
                });
            }
            catch (Exception ex)
            {
                // Safety net for async void
                System.Diagnostics.Debug.WriteLine($"Error handling TaskUpdatedMessage: {ex.Message}");
            }
        }

        public async void Receive(CreateTaskFromVariationOrderMessage message)
        {
            var variationOrder = message.VariationOrder;
            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            try
            {
                var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
                await vm.InitializeForCreation(ProjectId);
                
                // Prefill details from approved Variation Order
                vm.Task.Name = $"VO: {variationOrder.Description}";
                vm.Task.Description = $"Created from Variation Order: {variationOrder.Description}";
                if (!string.IsNullOrEmpty(variationOrder.AdditionalComments))
                {
                    vm.Task.Description += $"\n\nComments: {variationOrder.AdditionalComments}";
                }
                vm.Task.VariationOrderId = variationOrder.Id;
                
                // Set start/finish date based on VO's date and duration days
                vm.Task.StartDate = variationOrder.Date;
                if (variationOrder.DurationDays > 0)
                {
                    vm.Task.FinishDate = variationOrder.Date.AddDays(variationOrder.DurationDays);
                }
                else
                {
                    vm.Task.FinishDate = variationOrder.Date.AddDays(1);
                }
                
                vm.CloseFinished += (s, e) => CurrentTaskDetail = null;
                CurrentTaskDetail = vm;
            }
            catch (Exception ex)
            {
                toastService.ShowError("Error", "Could not initialize new task: " + ex.Message);
            }
        }
    }

    public partial class SubContractorSelectionViewModel : ObservableObject
    {
        public Guid Id { get; }
        public string Name { get; }
        public string Specialty { get; }
        public string Color { get; }
        public AssigneeType Type { get; set; } = AssigneeType.Contractor;

        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isVisible = true;

        public SubContractorSelectionViewModel(Guid id, string name, string specialty, string color)
        {
            Id = id;
            Name = name;
            Specialty = specialty;
            Color = color;
        }
    }
}
