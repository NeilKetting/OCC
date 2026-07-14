using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

using Microsoft.Extensions.DependencyInjection;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    /// <summary>
    /// ViewModel for the Project Gantt View, responsible for calculating layout coordinate and managing task visuals.
    /// </summary>
    public partial class ProjectGanttViewModel : ViewModelBase, IOverlayProvider
    {
        #region Private Members

        private readonly IProjectService _projectService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IProjectTaskService _taskService;
        private readonly IDialogService _dialogService;
        
        private List<ProjectTask> _rootTasks = new();
        private Guid _projectId;
        private List<ProjectTask> _allTasksFlat = new();
        /// <summary>Maps row-number (1-based) back to task ID string, for parsing user-typed predecessors.</summary>
        private Dictionary<int, string> _rowNumberToTaskId = new();
        /// <summary>Maps task ID string to row-number (1-based), for display.</summary>
        private Dictionary<string, int> _taskIdToRowNumber = new();

        #endregion

        #region Observables

        /// <summary>Active overlay task details drawer.</summary>
        [ObservableProperty] private TaskDetailViewModel? _currentTaskDetail;

        /// <summary>Currently selected task wrapper in the list.</summary>
        [ObservableProperty] private GanttTaskWrapper? _selectedTaskWrapper;

        public bool IsTaskSelected => SelectedTaskWrapper != null;

        partial void OnSelectedTaskWrapperChanged(GanttTaskWrapper? value)
        {
            OnPropertyChanged(nameof(IsTaskSelected));
            NewTaskCommand.NotifyCanExecuteChanged();
            CreateSubtaskCommand.NotifyCanExecuteChanged();
            EditTaskCommand.NotifyCanExecuteChanged();
            DeleteTaskCommand.NotifyCanExecuteChanged();
        }

        public ViewModelBase? ActiveOverlay => CurrentTaskDetail;

        /// <summary>
        /// Collection of wrapped tasks ready for rendering in the Gantt chart.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<GanttTaskWrapper> _ganttTasks = new();
        
        /// <summary>
        /// Current zoom level for the Gantt chart display.
        /// </summary>
        [ObservableProperty]
        private double _zoomLevel = 1.0;

        partial void OnZoomLevelChanged(double value)
        {
            PixelsPerDay = 50.0 * value;
            RefreshGanttView();
        }

        /// <summary>
        /// The calculated start date for the Gantt timeline.
        /// </summary>
        [ObservableProperty]
        private DateTime _projectStartDate = DateTime.Now;

        /// <summary>
        /// Number of pixels per day on the horizontal timeline.
        /// </summary>
        [ObservableProperty]
        private double _pixelsPerDay = 50.0;

        /// <summary>
        /// Height of each task row in pixels.
        /// </summary>
        [ObservableProperty]
        private double _rowHeight = 24.0;

        /// <summary>
        /// Total width of the Gantt canvas.
        /// </summary>
        [ObservableProperty]
        private double _canvasWidth = 3000;

        /// <summary>
        /// Total height of the Gantt canvas.
        /// </summary>
        [ObservableProperty]
        private double _canvasHeight = 600;

        /// <summary>
        /// Horizontal position of the 'Today' line.
        /// </summary>
        [ObservableProperty]
        private double _todayPosition;

        /// <summary>
        /// Date headers to display at the top of the Gantt chart.
        /// </summary>
        public ObservableCollection<GanttDateHeader> DateHeaders { get; } = new();

        /// <summary>
        /// Collection of dependency lines (arrows) between tasks.
        /// </summary>
        public ObservableCollection<GanttDependencyLine> Dependencies { get; } = new();

        // Smart Filters
        [ObservableProperty] private int _overdueCount;
        [ObservableProperty] private int _dueTodayCount;
        [ObservableProperty] private int _dueThisWeekCount;
        [ObservableProperty] private int _dueThisMonthCount;
        [ObservableProperty] private bool _isFilterPopupOpen;
        
        [ObservableProperty] private string _activeSmartFilter = "All Tasks"; // "All", "Overdue", "Today", "Week", "Month"
        [ObservableProperty] private Guid? _filterSubContractorId;

        /// <summary>Controls whether dependency connector lines are visible on the Gantt chart.</summary>
        [ObservableProperty] private bool _showDependencyLines = true;

        /// <summary>True while predecessor cascade changes have been computed but not yet saved.</summary>
        [ObservableProperty] private bool _hasPendingCascadeChanges;

        /// <summary>Width of the Predecessors column in the left panel. Bound by both header and row grids.</summary>
        [ObservableProperty] private double _predColumnWidth = 70;

        public ObservableCollection<OCC.Shared.DTOs.SubContractorFilterDto> SubContractorFilters { get; } = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Standard constructor for design-time support.
        /// </summary>
        public ProjectGanttViewModel()
        {
            _projectService = null!;
            _serviceProvider = null!;
            _taskService = null!;
            _dialogService = null!;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectGanttViewModel"/> with the required project manager.
        /// </summary>
        public ProjectGanttViewModel(
            IProjectService projectService,
            IServiceProvider serviceProvider,
            IProjectTaskService taskService,
            IDialogService dialogService)
        {
            _projectService = projectService;
            _serviceProvider = serviceProvider;
            _taskService = taskService;
            _dialogService = dialogService;
        }

        public override void Dispose()
        {
            base.Dispose();
            GanttTasks.Clear();
            DateHeaders.Clear();
            Dependencies.Clear();
            _rootTasks.Clear();
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task NewTask()
        {
            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            try
            {
                var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
                await vm.InitializeForCreation(_projectId);
                vm.CloseFinished += (s, e) => CurrentTaskDetail = null;
                CurrentTaskDetail = vm;
            }
            catch (Exception ex)
            {
                toastService.ShowError("Error", "Could not initialize new task: " + ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(IsTaskSelected))]
        private async Task CreateSubtask()
        {
            if (SelectedTaskWrapper == null) return;
            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            try
            {
                var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
                await vm.InitializeForCreation(_projectId, SelectedTaskWrapper.Task.Id);
                vm.CloseFinished += (s, e) => CurrentTaskDetail = null;
                CurrentTaskDetail = vm;
            }
            catch (Exception ex)
            {
                toastService.ShowError("Error", "Could not initialize sub-task: " + ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(IsTaskSelected))]
        private async Task EditTask()
        {
            if (SelectedTaskWrapper == null) return;
            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            try
            {
                var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
                vm.CloseFinished += (s, e) => CurrentTaskDetail = null;
                CurrentTaskDetail = vm; // Show drawer immediately
                
                await vm.LoadTaskById(SelectedTaskWrapper.Task.Id); // Load data in background
            }
            catch (Exception ex)
            {
                toastService.ShowError("Error", "Could not load task details: " + ex.Message);
                CurrentTaskDetail = null;
            }
        }

        [RelayCommand(CanExecute = nameof(IsTaskSelected))]
        private async Task DeleteTask()
        {
            if (SelectedTaskWrapper == null) return;

            var confirm = await _dialogService.ShowConfirmationAsync(
                "Delete Task", 
                $"Are you sure you want to delete task '{SelectedTaskWrapper.Task.Name}'?\n\nThis action cannot be undone.");

            if (!confirm) return;

            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            try
            {
                IsBusy = true;
                BusyText = "Deleting task...";
                await _taskService.DeleteTaskAsync(SelectedTaskWrapper.Task.Id);
                
                // Publish TaskUpdatedMessage to trigger UI reload across view models (including parent and task list)
                WeakReferenceMessenger.Default.Send(new TaskUpdatedMessage(SelectedTaskWrapper.Task.Id));
                
                SelectedTaskWrapper = null;
            }
            catch (Exception ex)
            {
                toastService.ShowError("Error", "Failed to delete task: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Toggles the expansion state of a specific task and refreshes the view.
        /// </summary>
        /// <param name="task">The task to toggle.</param>
        private void ToggleExpand(ProjectTask task)
        {
            if (task == null) return;
            _projectService.ToggleExpand(task);
            RefreshGanttView();
        }

        [RelayCommand]
        public void ZoomIn()
        {
            if (ZoomLevel < 3.0)
                ZoomLevel = Math.Round(ZoomLevel + 0.2, 1);
        }

        [RelayCommand]
        public void ZoomOut()
        {
            if (ZoomLevel > 0.4)
                ZoomLevel = Math.Round(ZoomLevel - 0.2, 1);
        }

        [RelayCommand]
        public void GoToToday()
        {
            WeakReferenceMessenger.Default.Send<OCC.WpfClient.Infrastructure.Messages.GanttScrollToDateMessage>(new OCC.WpfClient.Infrastructure.Messages.GanttScrollToDateMessage(DateTime.Now));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Updates the Gantt chart with the provided tasks.
        /// </summary>
        /// <param name="projectId">ID of the project.</param>
        /// <param name="tasks">The list of tasks to display.</param>
        public async Task UpdateTasksAsync(Guid projectId, List<ProjectTask> tasks, bool silent = false)
        {
            try
            {
                if (!silent)
                {
                    IsBusy = true;
                    BusyText = "Rendering Gantt Chart...";
                    
                    // Allow UI thread to show the spinner before we block it with heavy rendering
                    await Task.Delay(10);
                }

                _projectId = projectId;

                // Expand all by default to avoid large white space at bottom (User Workaround)
                foreach (var t in tasks) t.IsExpanded = true;
                
                _allTasksFlat = tasks;
                CalculateSmartStats(tasks);

                // 1. Build Hierarchy
                _rootTasks = _projectService.BuildTaskHierarchy(tasks);
                
                // 2. Refresh Visuals
                RefreshGanttView();
                HasPendingCascadeChanges = false;
            }
            finally
            {
                if (!silent)
                {
                    IsBusy = false;
                }
            }
        }

        /// <summary>
        /// Refreshes the Gantt chart visuals based on the current hierarchy and expansion states.
        /// </summary>
        private void RefreshGanttView()
        {
            var filteredRoots = _rootTasks.Where(t => MatchesFilter(t)).ToList();
            var visibleTasks = _projectService.FlattenHierarchy(filteredRoots);
            RebuildGanttTasks(visibleTasks);
        }

        private bool MatchesFilter(ProjectTask task)
        {
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

            return (matchesSmart && matchesSubbie) || anyChildMatches;
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
                    SubContractorFilters.Add(new OCC.Shared.DTOs.SubContractorFilterDto { Id = kvp.Key, Name = kvp.Value.Name, TaskCount = kvp.Value.Count });
                }
            });
        }

        [RelayCommand]
        private void ApplySmartFilter(string filter)
        {
            ActiveSmartFilter = filter;
            IsFilterPopupOpen = false;
            RefreshGanttView();
        }

        [RelayCommand]
        private void ApplySubContractorFilter(Guid? subbieId)
        {
            FilterSubContractorId = subbieId;
            IsFilterPopupOpen = false;
            RefreshGanttView();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            ActiveSmartFilter = "All Tasks";
            FilterSubContractorId = null;
            IsFilterPopupOpen = false;
            RefreshGanttView();
        }

        [RelayCommand]
        private void ToggleFilterPopup() => IsFilterPopupOpen = !IsFilterPopupOpen;

        /// <summary>Toggles the visibility of dependency connector lines on the chart.</summary>
        [RelayCommand]
        private void ToggleDependencyLines() => ShowDependencyLines = !ShowDependencyLines;

        /// <summary>
        /// Topologically sorts the flat task list and cascades start/finish dates based on
        /// Finish-to-Start (FS) predecessor relationships. Updates the Gantt visuals immediately.
        /// The user must then click Save to persist changes.
        /// </summary>
        [RelayCommand]
        private void ApplyPredecessorCascade()
        {
            if (_allTasksFlat.Count == 0) return;

            var lookup = _allTasksFlat.ToDictionary(t => t.Id.ToString());

            // Topological sort using Kahn's algorithm
            var inDegree = _allTasksFlat.ToDictionary(t => t.Id.ToString(), _ => 0);
            var dependents = new Dictionary<string, List<string>>();

            foreach (var task in _allTasksFlat)
            {
                string taskKey = task.Id.ToString();
                foreach (var predStr in task.Predecessors)
                {
                    var info = ParsePredecessor(predStr);
                    if (lookup.ContainsKey(info.PredecessorId))
                    {
                        inDegree[taskKey]++;
                        if (!dependents.ContainsKey(info.PredecessorId)) dependents[info.PredecessorId] = new List<string>();
                        dependents[info.PredecessorId].Add(taskKey);
                    }
                }
            }

            var queue = new Queue<string>(_allTasksFlat
                .Where(t => inDegree[t.Id.ToString()] == 0)
                .Select(t => t.Id.ToString()));

            var ordered = new List<ProjectTask>();
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!lookup.TryGetValue(id, out var task)) continue;
                ordered.Add(task);
                if (dependents.TryGetValue(id, out var deps))
                    foreach (var dep in deps)
                    {
                        if (--inDegree[dep] == 0) queue.Enqueue(dep);
                    }
            }

            // Cascade dates in topological order supporting FS, SS, FF, SF relationships with LagDays
            bool anyChanged = false;
            foreach (var task in ordered)
            {
                if (task.Predecessors.Count == 0) continue;

                DateTime earliestAllowedStart = DateTime.MinValue;
                foreach (var predStr in task.Predecessors)
                {
                    var info = ParsePredecessor(predStr);
                    if (lookup.TryGetValue(info.PredecessorId, out var pred))
                    {
                        DateTime candidateStart = DateTime.MinValue;
                        var duration = task.FinishDate - task.StartDate;

                        switch (info.Type)
                        {
                            case "FS":
                                candidateStart = pred.FinishDate.AddDays(info.LagDays);
                                break;
                            case "SS":
                                candidateStart = pred.StartDate.AddDays(info.LagDays);
                                break;
                            case "FF":
                                candidateStart = pred.FinishDate.AddDays(info.LagDays) - duration;
                                break;
                            case "SF":
                                candidateStart = pred.StartDate.AddDays(info.LagDays) - duration;
                                break;
                        }

                        if (candidateStart > earliestAllowedStart)
                        {
                            earliestAllowedStart = candidateStart;
                        }
                    }
                }

                if (earliestAllowedStart > DateTime.MinValue && task.StartDate < earliestAllowedStart)
                {
                    var duration = task.FinishDate - task.StartDate;
                    task.StartDate = earliestAllowedStart;
                    task.FinishDate = earliestAllowedStart + duration;
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                HasPendingCascadeChanges = true;
                _rootTasks = _projectService.BuildTaskHierarchy(_allTasksFlat);
                RefreshGanttView();
            }
        }

        /// <summary>Saves the predecessor-cascaded task dates to the API.</summary>
        [RelayCommand]
        private async Task SaveCascadeChanges()
        {
            if (!HasPendingCascadeChanges || _projectId == Guid.Empty) return;
            try
            {
                IsBusy = true;
                BusyText = "Saving schedule changes...";
                await _projectService.UpdateProjectTasksAsync(_projectId, _allTasksFlat);
                HasPendingCascadeChanges = false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save: {ex.Message}", "Save Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Public API for View

        /// <summary>
        /// Called by the view code-behind when the user finishes editing the PRED cell for a task.
        /// Parses comma-separated row numbers back to task IDs and updates the task's predecessor list.
        /// </summary>
        public void UpdateTaskPredecessors(string taskIdStr, string newPredText)
        {
            var task = _allTasksFlat.FirstOrDefault(t => t.Id.ToString() == taskIdStr);
            if (task == null) return;

            task.Predecessors.Clear();
            var parts = newPredText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var match = System.Text.RegularExpressions.Regex.Match(part.Trim(), @"^\s*(?<row>\d+)\s*(?<type>FS|SS|FF|SF)?\s*(?:(?<sign>[+-])\s*(?<lagValue>\d+(?:\.\d+)?)\s*(?:day|days|d|hour|hours|h)?)?\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    int rowNum = int.Parse(match.Groups["row"].Value);
                    if (_rowNumberToTaskId.TryGetValue(rowNum, out var predId))
                    {
                        string type = "FS";
                        if (match.Groups["type"].Success)
                        {
                            type = match.Groups["type"].Value.ToUpper();
                        }
                        
                        double lagDays = 0;
                        if (match.Groups["lagValue"].Success)
                        {
                            double.TryParse(match.Groups["lagValue"].Value, out var val);
                            string sign = match.Groups["sign"].Value;
                            if (sign == "-") val = -val;
                            lagDays = val;
                        }
                        
                        task.Predecessors.Add($"{predId}|{type}|{lagDays}");
                    }
                }
            }

            HasPendingCascadeChanges = true;

            // Update display text on the wrapper
            var wrapper = GanttTasks.FirstOrDefault(w => w.Task.Id.ToString() == taskIdStr);
            if (wrapper != null)
            {
                var displayParts = new List<string>();
                foreach (var predStr in task.Predecessors)
                {
                    var info = ParsePredecessor(predStr);
                    if (_taskIdToRowNumber.TryGetValue(info.PredecessorId, out var rowNum))
                    {
                        string part = rowNum.ToString();
                        if (info.Type != "FS" || info.LagDays != 0)
                        {
                            part += info.Type;
                            if (info.LagDays > 0)
                            {
                                part += $"+{info.LagDays} day" + (info.LagDays == 1 ? "" : "s");
                            }
                            else if (info.LagDays < 0)
                            {
                                part += $"{info.LagDays} day" + (info.LagDays == -1 ? "" : "s");
                            }
                        }
                        displayParts.Add(part);
                    }
                }
                wrapper.PredecessorText = string.Join(", ", displayParts);
            }

            // Update column width based on the edited text lengths
            double maxCharsAfterEdit = GanttTasks.Any() ? GanttTasks.Max(w => w.PredecessorText?.Length ?? 0) : 0;
            PredColumnWidth = Math.Max(70, maxCharsAfterEdit * 7.5 + 25);

            // Refresh dependency lines immediately
            var map = GanttTasks.ToDictionary(w => w.Task.Id.ToString());
            Dependencies.Clear();
            GenerateDependencies(map);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Rebuilds the collection of GanttTaskWrappers, calculating their visual positions.
        /// </summary>
        /// <param name="taskList">The flat list of visible tasks.</param>
        private void RebuildGanttTasks(List<ProjectTask> taskList)
        {
            GanttTasks.Clear();
            DateHeaders.Clear();
            Dependencies.Clear();
            
            DateTime minDate = DateTime.MaxValue;
            DateTime maxDate = DateTime.MinValue;

            foreach (var task in taskList)
            {
                 if (task.StartDate > DateTime.MinValue && task.StartDate < minDate) minDate = task.StartDate;
                 if (task.FinishDate > DateTime.MinValue && task.FinishDate > maxDate) maxDate = task.FinishDate;
            }

            // Timeline Padding
            if (minDate != DateTime.MaxValue)
                ProjectStartDate = minDate.AddDays(-7); 
            else
                ProjectStartDate = DateTime.Now.AddDays(-14);

            if (maxDate == DateTime.MinValue) maxDate = ProjectStartDate.AddDays(30);

            GenerateTimelineHeaders(ProjectStartDate, maxDate.AddDays(30));

            // Calculate Today Position
            TodayPosition = (DateTime.Now - ProjectStartDate).TotalDays * PixelsPerDay;

            var days = (maxDate.AddDays(30) - ProjectStartDate).TotalDays;
            CanvasWidth = Math.Max(3000, days * PixelsPerDay);

            int index = 0;
            double topPadding = 4.0; 
            
            var idToWrapperMap = new Dictionary<string, GanttTaskWrapper>();

            // Build row-number maps (used for PRED column display and editing)
            _taskIdToRowNumber = taskList
                .Select((t, i) => new { Key = t.Id.ToString(), Row = i + 1 })
                .ToDictionary(x => x.Key, x => x.Row);
            _rowNumberToTaskId = _taskIdToRowNumber.ToDictionary(kv => kv.Value, kv => kv.Key);
            
            foreach (var task in taskList)
            {
                var wrapper = new GanttTaskWrapper(task, ProjectStartDate, PixelsPerDay, index, topPadding, RowHeight, _taskIdToRowNumber);
                wrapper.ToggleCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => ToggleExpand(task));
                
                GanttTasks.Add(wrapper);
                idToWrapperMap[task.Id.ToString()] = wrapper;
                index++;
            }
            
            CanvasHeight = Math.Max(600, index * RowHeight + 100);

            // Calculate auto width for the PRED column based on content length to prevent clipping
            double maxChars = GanttTasks.Any() ? GanttTasks.Max(w => w.PredecessorText?.Length ?? 0) : 0;
            PredColumnWidth = Math.Max(70, maxChars * 7.5 + 25);

            GenerateDependencies(idToWrapperMap);
            HarmonizeVisualDates(GanttTasks.ToList());
        }

        /// <summary>
        /// Adjusts summary task dates and positions to encompass their children's visual spans.
        /// </summary>
        private void HarmonizeVisualDates(List<GanttTaskWrapper> wrappers)
        {
            var parentStack = new Stack<GanttTaskWrapper>();
            
            foreach (var wrapper in wrappers)
            {
                while (parentStack.Count > 0 && parentStack.Peek().Task.IndentLevel >= wrapper.Task.IndentLevel)
                {
                    parentStack.Pop();
                }
                
                if (parentStack.Count > 0)
                {
                    parentStack.Peek().ChildrenWrappers.Add(wrapper);
                }
                
                parentStack.Push(wrapper);
            }
            
            // Bubble up visual bounds
            for (int i = wrappers.Count - 1; i >= 0; i--)
            {
                var wrapper = wrappers[i];
                if (wrapper.ChildrenWrappers.Count > 0)
                {
                    double minLeft = double.MaxValue;
                    double maxRight = double.MinValue;
                    bool hasChildren = false;

                    foreach (var child in wrapper.ChildrenWrappers)
                    {
                        if (child.Left < minLeft) minLeft = child.Left;
                        if (child.Right > maxRight) maxRight = child.Right;
                        hasChildren = true;
                    }
                    
                    if (hasChildren && minLeft != double.MaxValue && maxRight != double.MinValue)
                    {
                         wrapper.Left = minLeft;
                         wrapper.Width = maxRight - minLeft;
                         if (wrapper.Width < 10) wrapper.Width = 20; 
                    }
                }
            }
        }

        /// <summary>
        /// Generates the visual dependency lines based on task predecessor data.
        /// </summary>
        private void GenerateDependencies(Dictionary<string, GanttTaskWrapper> map)
        {
            foreach (var wrapper in GanttTasks)
            {
                foreach (var predString in wrapper.Task.Predecessors)
                {
                    var info = ParsePredecessor(predString);
                    if (map.TryGetValue(info.PredecessorId, out var predWrapper))
                    {
                        Dependencies.Add(new GanttDependencyLine(predWrapper, wrapper, info.Type));
                    }
                }
            }
        }
        
        /// <summary>
        /// Generates the day headers and vertical grid markers for the timeline.
        /// </summary>
        private void GenerateTimelineHeaders(DateTime start, DateTime end)
        {
            DateHeaders.Clear();
            var current = start;
            int index = 0;
            while (current <= end)
            {
                double left = (current - ProjectStartDate).TotalDays * PixelsPerDay;
                DateHeaders.Add(new GanttDateHeader 
                { 
                    Text = current.ToString("dd MMM"),
                    Left = left + 5,
                    ColumnLeft = left,
                    Width = PixelsPerDay,
                    IsAlternate = (index % 2 == 1)
                });
                current = current.AddDays(1);
                index++;
            }
        }

        /// <summary>
        /// Parses a predecessor storage string ("guid|type|lag") into structured PredecessorInfo.
        /// Handles legacy formats ("guid|1" or just "guid").
        /// </summary>
        public static PredecessorInfo ParsePredecessor(string predStr)
        {
            var info = new PredecessorInfo();
            if (string.IsNullOrEmpty(predStr)) return info;

            var parts = predStr.Split('|');
            info.PredecessorId = parts[0];

            if (parts.Length > 1)
            {
                var typePart = parts[1].Trim().ToUpper();
                if (typePart == "0" || typePart == "FF") info.Type = "FF";
                else if (typePart == "1" || typePart == "FS") info.Type = "FS";
                else if (typePart == "2" || typePart == "SS") info.Type = "SS";
                else if (typePart == "3" || typePart == "SF") info.Type = "SF";
                else info.Type = "FS";
            }

            if (parts.Length > 2)
            {
                if (double.TryParse(parts[2], out var lag))
                {
                    info.LagDays = lag;
                }
            }

            return info;
        }

        #endregion
    }

    /// <summary>
    /// Represents a dependency connector line between two Gantt tasks, using MS Project-style routing:
    /// Supports FS, SS, FF, SF relationships with custom alignments and directional arrowheads.
    /// </summary>
    public class GanttDependencyLine
    {
        public System.Windows.Media.StreamGeometry PathGeometry { get; private set; }
        public System.Windows.Media.StreamGeometry ArrowGeometry { get; private set; }

        private const double HStub = 8.0;   // horizontal stub length leaving/entering a bar
        private const double ArrowSize = 6.0;

        public GanttDependencyLine(GanttTaskWrapper predecessor, GanttTaskWrapper successor, string typeStr)
        {
            double predLeft = predecessor.Left;
            double predRight = predecessor.Left + predecessor.Width;
            double predMidY  = predecessor.Top + predecessor.Height / 2.0;

            double succLeft  = successor.Left;
            double succRight = successor.Left + successor.Width;
            double succMidY  = successor.Top + successor.Height / 2.0;

            // Determine origin and destination based on relationship type
            double startX = predRight;
            double endX = succLeft;
            bool enterFromLeft = true; 
            bool exitToRight = true;

            switch (typeStr)
            {
                case "SS":
                    startX = predLeft;
                    endX = succLeft;
                    exitToRight = false;
                    enterFromLeft = true;
                    break;
                case "FF":
                    startX = predRight;
                    endX = succRight;
                    exitToRight = true;
                    enterFromLeft = false;
                    break;
                case "SF":
                    startX = predLeft;
                    endX = succRight;
                    exitToRight = false;
                    enterFromLeft = false;
                    break;
                case "FS":
                default:
                    startX = predRight;
                    endX = succLeft;
                    exitToRight = true;
                    enterFromLeft = true;
                    break;
            }

            var geometry = new System.Windows.Media.StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var p1 = new System.Windows.Point(startX, predMidY);
                double stub1 = exitToRight ? HStub : -HStub;
                double stub2 = enterFromLeft ? -HStub : HStub;

                var p2 = new System.Windows.Point(startX + stub1, predMidY);
                var p4 = new System.Windows.Point(endX + stub2, succMidY);
                var p5 = new System.Windows.Point(endX, succMidY);

                ctx.BeginFigure(p1, false, false);

                if ((exitToRight && enterFromLeft && (endX + stub2 >= startX + stub1)) ||
                    (!exitToRight && !enterFromLeft && (endX + stub2 <= startX + stub1)))
                {
                    // Clean path with enough clearance between stubs
                    var p3 = new System.Windows.Point(startX + stub1, succMidY);
                    ctx.LineTo(p2, true, false);
                    ctx.LineTo(p3, true, false);
                    ctx.LineTo(p4, true, false);
                    ctx.LineTo(p5, true, false);
                }
                else
                {
                    // Bypass path when there's an overlap or opposite direction
                    double midY = (predMidY + succMidY) / 2.0;
                    if (Math.Abs(predMidY - succMidY) < 4) midY = predMidY + predecessor.Height;
                    
                    ctx.LineTo(p2, true, false);
                    ctx.LineTo(new System.Windows.Point(startX + stub1, midY), true, false);
                    ctx.LineTo(new System.Windows.Point(endX + stub2, midY), true, false);
                    ctx.LineTo(p4, true, false);
                    ctx.LineTo(p5, true, false);
                }
            }
            geometry.Freeze();
            PathGeometry = geometry;

            var arrow = new System.Windows.Media.StreamGeometry();
            using (var ctx = arrow.Open())
            {
                var tip = new System.Windows.Point(endX, succMidY);
                System.Windows.Point topPt, botPt;
                if (enterFromLeft)
                {
                    topPt = new System.Windows.Point(endX - ArrowSize, succMidY - ArrowSize * 0.5);
                    botPt = new System.Windows.Point(endX - ArrowSize, succMidY + ArrowSize * 0.5);
                }
                else
                {
                    topPt = new System.Windows.Point(endX + ArrowSize, succMidY - ArrowSize * 0.5);
                    botPt = new System.Windows.Point(endX + ArrowSize, succMidY + ArrowSize * 0.5);
                }
                ctx.BeginFigure(tip, true, true);
                ctx.LineTo(topPt, true, false);
                ctx.LineTo(botPt, true, false);
            }
            arrow.Freeze();
            ArrowGeometry = arrow;
        }
    }

    /// <summary>
    /// Represents a single day header in the Gantt chart timeline.
    /// </summary>
    public class GanttDateHeader
    {
        public string Text { get; set; } = string.Empty;
        public double Left { get; set; }
        public double ColumnLeft { get; set; }
        public double Width { get; set; }
        public bool IsAlternate { get; set; }
    }

    /// <summary>
    /// Wrapper for a ProjectTask that adds visual positioning properties for the Gantt chart.
    /// </summary>
    public class GanttTaskWrapper : ObservableObject
    {
        public ProjectTask Task { get; }
        
        private double _left;
        public double Left 
        { 
            get => _left; 
            set => SetProperty(ref _left, value); 
        }

        private double _width;
        public double Width 
        { 
            get => _width; 
            set {
                if (SetProperty(ref _width, value))
                {
                    OnPropertyChanged(nameof(Right));
                }
            }
        }
        
        public double Right => Left + Width;
        public double Top { get; }
        public double Height { get; } = 20;
        public bool IsSummary { get; }
        public bool IsAlternate { get; } 
        public string LabelText { get; } 
        public double RowHeight { get; }
        public double RowTop { get; }
        
        public CommunityToolkit.Mvvm.Input.RelayCommand? ToggleCommand { get; set; } 
        public System.Windows.Thickness IndentMargin { get; }
        public bool HasChildren { get; }
        public List<GanttTaskWrapper> ChildrenWrappers { get; } = new();

        /// <summary>Display text for the Predecessors column (row numbers, comma-separated). Observable so edits reflect immediately.</summary>
        private string _predecessorText = string.Empty;
        public string PredecessorText
        {
            get => _predecessorText;
            set => SetProperty(ref _predecessorText, value);
        }

        public int RowNumber { get; }

        public GanttTaskWrapper(ProjectTask task, DateTime projectStart, double pixelsPerDay, int index, double topOffset, double rowHeight,
            Dictionary<string, int>? taskIdToRowNumber = null)
        {
            RowNumber = index + 1;
            Task = task;
            IsSummary = task.IsGroup;
            HasChildren = task.Children.Any();
            IsAlternate = index % 2 != 0;
            IndentMargin = new System.Windows.Thickness(task.IndentLevel * 15, 0, 0, 0);
            
            string resources = string.Join(", ", task.Assignments?.Select(a => a.AssigneeName) ?? Enumerable.Empty<string>());
            LabelText = $"{task.Name}  {task.PercentComplete}%  {resources}";

            // Build predecessor text using row numbers and types/lags (like MS Project)
            if (taskIdToRowNumber != null && task.Predecessors.Count > 0)
            {
                var displayParts = new List<string>();
                foreach (var predStr in task.Predecessors)
                {
                    var info = ProjectGanttViewModel.ParsePredecessor(predStr);
                    if (taskIdToRowNumber.TryGetValue(info.PredecessorId, out var rowNum))
                    {
                        string part = rowNum.ToString();
                        if (info.Type != "FS" || info.LagDays != 0)
                        {
                            part += info.Type;
                            if (info.LagDays > 0)
                            {
                                part += $"+{info.LagDays} day" + (info.LagDays == 1 ? "" : "s");
                            }
                            else if (info.LagDays < 0)
                            {
                                part += $"{info.LagDays} day" + (info.LagDays == -1 ? "" : "s");
                            }
                        }
                        displayParts.Add(part);
                    }
                }
                PredecessorText = string.Join(", ", displayParts);
            }
            
            var startOffset = (task.StartDate - projectStart).TotalDays;
            if (startOffset < 0) startOffset = 0;
            
            _left = startOffset * pixelsPerDay;
            
            var durationDays = (task.FinishDate - task.StartDate).TotalDays;
            if (durationDays < 0.5) durationDays = 1.0; 
            
            _width = durationDays * pixelsPerDay;
            
            RowHeight = rowHeight;
            RowTop = index * rowHeight;
            Top = RowTop + topOffset; 
        }
    }

    /// <summary>
    /// Holds parsed predecessor link information.
    /// </summary>
    public class PredecessorInfo
    {
        public string PredecessorId { get; set; } = string.Empty;
        public string Type { get; set; } = "FS"; // FS, SS, FF, SF
        public double LagDays { get; set; } = 0;
    }
}
