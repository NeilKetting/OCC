using Avalonia.Controls;
using DashboardVM = OCC.Client.Features.HomeHub.ViewModels.Dashboard;

namespace OCC.Client.Features.HomeHub.Views.MySummary.Widgets
{
    public partial class TasksWidgetView : UserControl
    {
        public TasksWidgetView()
        {
            InitializeComponent();
        }

        private void OnTaskDoubleTapping(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is OCC.Client.Features.HomeHub.ViewModels.Shared.HomeTaskItem task && DataContext is DashboardVM.TasksWidgetViewModel vmModel)
            {
                vmModel.OpenTaskCommand.Execute(task);
            }
        }

        private void OnOpenClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is OCC.Client.Features.HomeHub.ViewModels.Shared.HomeTaskItem item && DataContext is DashboardVM.TasksWidgetViewModel vm)
            {
                vm.OpenTaskCommand.Execute(item);
            }
        }

        private void OnCompleteClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is OCC.Client.Features.HomeHub.ViewModels.Shared.HomeTaskItem item && DataContext is DashboardVM.TasksWidgetViewModel vm)
            {
                vm.CompleteTaskCommand.Execute(item);
            }
        }

        private void OnDeleteClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is OCC.Client.Features.HomeHub.ViewModels.Shared.HomeTaskItem item && DataContext is DashboardVM.TasksWidgetViewModel vm)
            {
                vm.DeleteTaskCommand.Execute(item);
            }
        }
    }
}
