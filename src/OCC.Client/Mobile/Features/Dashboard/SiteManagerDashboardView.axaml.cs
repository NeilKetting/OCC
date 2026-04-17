using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OCC.Client.Mobile.Features.Dashboard;

namespace OCC.Client.Mobile.Features.Dashboard
{
    public partial class SiteManagerDashboardView : UserControl
    {
        public SiteManagerDashboardView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            
            if (DataContext is SiteManagerDashboardViewModel vm)
            {
                _ = vm.LoadProjectsAsync();
            }
        }
    }
}
