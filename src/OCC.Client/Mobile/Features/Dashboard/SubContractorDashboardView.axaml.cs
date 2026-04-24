using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Mobile.Features.Dashboard
{
    public partial class SubContractorDashboardView : UserControl
    {
        public SubContractorDashboardView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
