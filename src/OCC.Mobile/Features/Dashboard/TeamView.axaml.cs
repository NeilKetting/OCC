using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class TeamView : UserControl
    {
        public TeamView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
