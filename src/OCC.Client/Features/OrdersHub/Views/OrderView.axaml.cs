using OCC.Client.Features.OrdersHub.ViewModels;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.OrdersHub.Views
{
    public partial class OrderView : UserControl
    {
        public OrderView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
