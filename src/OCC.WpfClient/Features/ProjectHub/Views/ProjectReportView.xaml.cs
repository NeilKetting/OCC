using System.Windows.Controls;

namespace OCC.WpfClient.Features.ProjectHub.Views
{
    public partial class ProjectReportView : UserControl
    {
        public ProjectReportView()
        {
            InitializeComponent();
        }

        private void PrintButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.DataContext = btn.DataContext;
                btn.ContextMenu.IsOpen = true;
            }
        }
    }
}
