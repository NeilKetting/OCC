using System.Windows.Controls;
using System.Windows.Input;

namespace OCC.WpfClient.Features.ProjectHub.Views
{
    public partial class ProjectTaskListView : UserControl
    {
        public ProjectTaskListView()
        {
            InitializeComponent();
        }

        private void OnTaskDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.ProjectTaskListViewModel vm && vm.SelectedTask != null)
            {
                vm.EditTaskCommand.Execute(vm.SelectedTask);
            }
        }

        private void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.ProjectTaskListViewModel vm)
            {
                vm.CurrentTaskDetail = null;
            }
        }

        private void OnDrawerMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Prevent clicking inside the drawer from closing it
            e.Handled = true;
        }
    }
}
