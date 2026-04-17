using OCC.Client.Features.OrdersHub.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.OrdersHub.Views
{
    public partial class RestockReviewView : UserControl
    {
        public RestockReviewView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
