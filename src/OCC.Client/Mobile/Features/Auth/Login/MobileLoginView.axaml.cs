using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Mobile.Features.Auth.Login
{
    public partial class MobileLoginView : UserControl
    {
        public MobileLoginView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
