using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.HseqHub.Views
{
    public partial class HealthSafetyDashboardView : UserControl
    {
        public HealthSafetyDashboardView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
