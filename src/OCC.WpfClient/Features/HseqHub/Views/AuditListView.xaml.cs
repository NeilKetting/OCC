using System.Windows.Controls;
using OCC.WpfClient.Infrastructure;
using VM = OCC.WpfClient.Features.HseqHub.ViewModels;

namespace OCC.WpfClient.Features.HseqHub.Views
{
    public partial class AuditListView : ListViewBase
    {
        public AuditListView()
        {
            InitializeComponent();
        }

        private void NewAuditButton_Click(object sender, System.Windows.RoutedEventArgs e)
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
