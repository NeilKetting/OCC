using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class InventoryView : UserControl
    {
        public InventoryView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
