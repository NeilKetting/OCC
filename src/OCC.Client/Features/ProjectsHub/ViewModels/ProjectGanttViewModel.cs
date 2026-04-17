using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    /// <summary>
    /// ViewModel for the Project Gantt View, responsible for calculating layout coordinate and managing task visuals.
    /// </summary>
    public partial class ProjectGanttViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IProjectManager _projectManager;
        private List<ProjectTask> _rootTasks = new();

        #endregion

        #region Observables

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
        /// Date headers to display at the top of the Gantt chart.
        /// </summary>
        public ObservableCollection<GanttDateHeader> DateHeaders { get; } = new();

        /// <summary>
        /// Collection of dependency lines (arrows) between tasks.
        /// </summary>
        public ObservableCollection<GanttDependencyLine> Dependencies { get; } = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Standard constructor for design-time support.
        /// </summary>
        public ProjectGanttViewModel()
        {
            _projectManager = null!;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectGanttViewModel"/> with the required project manager.
        /// </summary>
        /// <param name="projectManager">The service for project and task logic.</param>
        public ProjectGanttViewModel(IProjectManager projectManager)
        {
            _projectManager = projectManager;
        }

        #endregion

        #region Commands

        /// <summary>
        /// Toggles the expansion state of a specific task and refreshes the view.
        /// </summary>
        /// <param name="task">The task to toggle.</param>
        private void ToggleExpand(ProjectTask task)
        {
            if (task == null) return;
            _projectManager.ToggleExpand(task);
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

        #endregion

        #region Methods

        /// <summary>
        /// Loads tasks for a project, builds the hierarchy, and prepares the Gantt visuals.
        /// </summary>
        /// <param name="projectId">ID of the project to load.</param>
        public async void LoadTasks(Guid projectId)
        {
            var tasks = await _projectManager.GetTasksForProjectAsync(projectId);
            
            // Expand all by default to avoid large white space at bottom (User Workaround)
            foreach (var t in tasks) t.IsExpanded = true;
            
            // 1. Build Hierarchy
            _rootTasks = _projectManager.BuildTaskHierarchy(tasks);
            
            // 2. Refresh Visuals
            RefreshGanttView();
        }

        /// <summary>
        /// Refreshes the Gantt chart visuals based on the current hierarchy and expansion states.
        /// </summary>
        private void RefreshGanttView()
        {
            var visibleTasks = _projectManager.FlattenHierarchy(_rootTasks);
            RebuildGanttTasks(visibleTasks);
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

            var days = (maxDate.AddDays(30) - ProjectStartDate).TotalDays;
            CanvasWidth = Math.Max(3000, days * PixelsPerDay);

            int index = 0;
            double topPadding = 4.0; 
            
            var idToWrapperMap = new Dictionary<string, GanttTaskWrapper>();
            
            foreach (var task in taskList)
            {
                var wrapper = new GanttTaskWrapper(task, ProjectStartDate, PixelsPerDay, index, topPadding, RowHeight);
                wrapper.ToggleCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => ToggleExpand(task));
                
                GanttTasks.Add(wrapper);
                idToWrapperMap[task.Id.ToString()] = wrapper;
                index++;
            }
            
            CanvasHeight = Math.Max(600, index * RowHeight + 100);

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
                    var parts = predString.Split('|');
                    var predId = parts[0];
                    int type = 1; // Default FS
                    if (parts.Length > 1 && int.TryParse(parts[1], out var t)) type = t;

                    if (map.TryGetValue(predId, out var predWrapper))
                    {
                        Dependencies.Add(new GanttDependencyLine(predWrapper, wrapper, type));
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

        #endregion
    }

    /// <summary>
    /// Represents a dependency line (arrow) between two tasks in the Gantt chart.
    /// </summary>
    public class GanttDependencyLine
    {
        public Avalonia.Media.StreamGeometry PathGeometry { get; private set; }
        public Avalonia.Media.StreamGeometry ArrowGeometry { get; private set; }

        public GanttDependencyLine(GanttTaskWrapper predecessor, GanttTaskWrapper successor, int type)
        {
            var start = new Avalonia.Point(predecessor.Left + predecessor.Width, predecessor.Top + (predecessor.Height / 2));
            var end = new Avalonia.Point(successor.Left, successor.Top + (successor.Height / 2));

            var geometry = new Avalonia.Media.StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(start, false);

                if (end.X > start.X + 20)
                {
                    double midX = start.X + (end.X - start.X) / 2;
                    context.LineTo(new Avalonia.Point(midX, start.Y));
                    context.LineTo(new Avalonia.Point(midX, end.Y));
                    context.LineTo(end);
                }
                else
                {
                    double midY = (start.Y + end.Y) / 2;
                    if (Math.Abs(start.Y - end.Y) < 10) midY = start.Y + 15;
                    context.LineTo(new Avalonia.Point(start.X + 10, start.Y));
                    context.LineTo(new Avalonia.Point(start.X + 10, midY));
                    context.LineTo(new Avalonia.Point(end.X - 10, midY));
                    context.LineTo(new Avalonia.Point(end.X - 10, end.Y));
                    context.LineTo(end);
                }
            }
            PathGeometry = geometry;

            var arrow = new Avalonia.Media.StreamGeometry();
            using (var ctx = arrow.Open())
            {
                ctx.BeginFigure(end, true);
                ctx.LineTo(new Avalonia.Point(end.X - 6, end.Y - 3));
                ctx.LineTo(new Avalonia.Point(end.X - 6, end.Y + 3));
                ctx.EndFigure(true);
            }
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
        public Avalonia.Thickness IndentMargin { get; }
        public bool HasChildren { get; }
        public List<GanttTaskWrapper> ChildrenWrappers { get; } = new();

        public GanttTaskWrapper(ProjectTask task, DateTime projectStart, double pixelsPerDay, int index, double topOffset, double rowHeight)
        {
            Task = task;
            IsSummary = task.IsGroup;
            HasChildren = task.Children.Any();
            IsAlternate = index % 2 != 0;
            IndentMargin = new Avalonia.Thickness(task.IndentLevel * 15, 0, 0, 0);
            
            string resources = string.Join(", ", task.Assignments?.Select(a => a.AssigneeName) ?? Enumerable.Empty<string>());
            LabelText = $"{task.Name}  {task.PercentComplete}%  {resources}";
            
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
}




