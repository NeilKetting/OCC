using OCC.Client.Features.WagesHub.ViewModels;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.WagesHub.Views
{
    public partial class WageRunView : UserControl
    {
        public WageRunView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
