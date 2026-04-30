using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Mobile.Features.Profile
{
    public partial class ProfileView : UserControl
    {
        public ProfileView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
