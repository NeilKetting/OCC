using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.SettingsHub.Views
{
    public partial class UserPreferencesView : UserControl
    {
        public UserPreferencesView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
