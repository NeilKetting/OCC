using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace OCC.Mobile.Features.Tasks
{
    public partial class RedesignTasksView : UserControl
    {
        public RedesignTasksView()
        {
            InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is RedesignTasksViewModel vm)
            {
                vm.ProjectGroups.CollectionChanged += ProjectGroups_CollectionChanged;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (DataContext is RedesignTasksViewModel vm)
            {
                vm.ProjectGroups.CollectionChanged -= ProjectGroups_CollectionChanged;
            }
            base.OnDetachedFromVisualTree(e);
        }

        private void ProjectGroups_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                if (DataContext is RedesignTasksViewModel vm)
                {
                    vm.ProjectGroups.CollectionChanged -= ProjectGroups_CollectionChanged;
                }
                
                TriggerScrollToTarget();
            }
        }

        private async void TriggerScrollToTarget()
        {
            // Wait for containers to be generated and transition animation to settle
            await System.Threading.Tasks.Task.Delay(500);
            
            var targetRow = FindExpandedTaskRow(this);
            if (targetRow != null)
            {
                // Run a series of scroll attempts to ensure it settles in the correct place
                for (int i = 0; i < 6; i++)
                {
                    if (TopLevel.GetTopLevel(targetRow) == null) break;
                    
                    targetRow.BringIntoView();
                    
                    await System.Threading.Tasks.Task.Delay(200);
                }
            }
        }

        private Control? FindExpandedTaskRow(Visual parent)
        {
            foreach (var child in parent.GetVisualChildren())
            {
                if (child is Control c && c.DataContext is RedesignTaskRowViewModel vm && vm.IsExpanded)
                {
                    return c;
                }
                
                var found = FindExpandedTaskRow(child);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void DetailPanel_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Control control)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (TopLevel.GetTopLevel(control) != null)
                    {
                        control.BringIntoView();
                    }
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
    }
}
