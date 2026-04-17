using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.CoreHub.Views
{
    public partial class SplashView : Window
    {
        public SplashView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
