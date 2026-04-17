using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Views.Shared
{
    public partial class ChangeEmailPopupView : UserControl
    {
        public ChangeEmailPopupView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
