using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

        /// <summary>
        /// Commits the predecessor edit when the TextBox loses focus.
        /// </summary>
        private void PredTextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            CommitPredEdit(sender as TextBox);
        }

        /// <summary>
        /// Commits on Enter, cancels (restores original) on Escape.
        /// </summary>
        private void PredTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox tb) return;

            if (e.Key == Key.Enter)
            {
                CommitPredEdit(tb);
                // Move focus away so the next row can be edited cleanly
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Revert to the current stored value
                if (tb.DataContext is GanttTaskWrapper wrapper)
                    tb.Text = wrapper.PredecessorText;
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void CommitPredEdit(TextBox? tb)
        {
            if (tb == null) return;
            if (DataContext is not ProjectGanttViewModel vm) return;
            if (tb.DataContext is not GanttTaskWrapper wrapper) return;

            vm.UpdateTaskPredecessors(wrapper.Task.Id.ToString(), tb.Text.Trim());
        }

        /// <summary>
        /// Updates PredColumnWidth on the ViewModel as the user drags the PRED column header splitter.
        /// </summary>
        private void PredColumnSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (DataContext is not ProjectGanttViewModel vm) return;
            // Dragging left (negative HorizontalChange) grows the PRED column
            double newWidth = vm.PredColumnWidth - e.HorizontalChange;
            vm.PredColumnWidth = Math.Max(40, Math.Min(200, newWidth));
        }

        private void TaskRow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement fe && fe.DataContext is GanttTaskWrapper wrapper)
            {
                if (DataContext is ProjectGanttViewModel vm)
                {
                    vm.SelectedTaskWrapper = wrapper;
                    if (e.ClickCount == 2)
                    {
                        vm.EditTaskCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        }
    }
}

