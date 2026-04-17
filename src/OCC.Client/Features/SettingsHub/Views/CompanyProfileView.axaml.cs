using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.SettingsHub.Views
{
    public partial class CompanyProfileView : UserControl
    {
        public CompanyProfileView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
