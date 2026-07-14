using System.Windows.Controls;
using OCC.WpfClient.Features.EmployeeHub.ViewModels;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Features.EmployeeHub.Views
{
    public partial class EmployeeListView : ListViewBase
    {
        public EmployeeListView()
        {
            InitializeComponent();
            this.SizeChanged += EmployeeListView_SizeChanged;
        }

        private void EmployeeListView_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            double availableWidth = e.NewSize.Width;
            if (availableWidth >= 1600)
            {
                DrawerWidth = 1100;
            }
            else if (availableWidth >= 1200)
            {
                DrawerWidth = 800;
            }
            else
            {
                DrawerWidth = 550;
            }
        }

        private void PrintButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }
    }
}
