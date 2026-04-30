using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class ActiveProjectsView : UserControl
    {
        public ActiveProjectsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
