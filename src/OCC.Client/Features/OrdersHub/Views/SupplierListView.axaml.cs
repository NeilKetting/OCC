using OCC.Client.Features.OrdersHub.ViewModels;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.OrdersHub.Views
{
    public partial class SupplierListView : UserControl
    {
        public SupplierListView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
