using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.BugHub.Views
{
    public partial class SupportCenterView : UserControl
    {
        public SupportCenterView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
