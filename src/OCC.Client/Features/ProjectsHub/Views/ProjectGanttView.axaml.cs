using Avalonia.Controls;

namespace OCC.Client.Features.ProjectsHub.Views
{
    public partial class ProjectGanttView : UserControl
    {
        private ScrollViewer? _headerScroll;
        private ScrollViewer? _taskScroll;
        private ScrollViewer? _ganttScroll;

        public ProjectGanttView()
        {
            InitializeComponent();
            
            // Allow time for template to apply
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
             _headerScroll = this.FindControl<ScrollViewer>("HeaderScrollViewer");
             _taskScroll = this.FindControl<ScrollViewer>("TaskScrollViewer");
             _ganttScroll = this.FindControl<ScrollViewer>("GanttScrollViewer");

             if (_ganttScroll != null)
             {
                 _ganttScroll.ScrollChanged += GanttScroll_ScrollChanged;
                 // Reset and Sync
                 _ganttScroll.Offset = Avalonia.Vector.Zero;
             }
             
             if (_taskScroll != null)
             {
                 _taskScroll.ScrollChanged += TaskScroll_ScrollChanged;
                 _taskScroll.Offset = Avalonia.Vector.Zero;
             }

             if (_headerScroll != null)
             {
                 _headerScroll.Offset = Avalonia.Vector.Zero;
             }
        }

        private void GanttScroll_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_headerScroll != null)
                _headerScroll.Offset = new Avalonia.Vector(_ganttScroll!.Offset.X, 0);
            
            if (_taskScroll != null)
                _taskScroll.Offset = new Avalonia.Vector(0, _ganttScroll!.Offset.Y);
        }

        private void TaskScroll_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            // Allow scrolling the task list to drive the gantt chart vertical scroll
            if (_ganttScroll != null)
                 _ganttScroll.Offset = new Avalonia.Vector(_ganttScroll.Offset.X, _taskScroll!.Offset.Y);
        }
    }
}




