using System.Windows.Controls;
using OCC.WpfClient.Features.ProjectHub.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;
using System;

namespace OCC.WpfClient.Features.ProjectHub.Views
{
    public partial class ProjectGanttView : UserControl, IRecipient<GanttScrollToDateMessage>
    {
        public ProjectGanttView()
        {
            InitializeComponent();
            
            WeakReferenceMessenger.Default.Register<GanttScrollToDateMessage>(this);

            // Wire up scrolling synchronization
            TaskListScrollViewer.ScrollChanged += (s, e) => {
                if (e.VerticalChange != 0)
                {
                    GanttChartScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
                }
            };
            
            GanttChartScrollViewer.ScrollChanged += (s, e) => {
                if (e.VerticalChange != 0)
                {
                    TaskListScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
                }
                if (e.HorizontalChange != 0)
                {
                    HeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
                }
            };
        }

        public void Receive(GanttScrollToDateMessage message)
        {
            if (DataContext is ProjectGanttViewModel vm)
            {
                var offset = (message.TargetDate - vm.ProjectStartDate).TotalDays * vm.PixelsPerDay;
                // Center it roughly
                var viewportWidth = GanttChartScrollViewer.ViewportWidth;
                GanttChartScrollViewer.ScrollToHorizontalOffset(offset - (viewportWidth / 2));
            }
        }
    }
}
