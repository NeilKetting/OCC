using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Mobile.Features.Dashboard
{
    public partial class MobileProjectDetailView : UserControl
    {
        public MobileProjectDetailView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
