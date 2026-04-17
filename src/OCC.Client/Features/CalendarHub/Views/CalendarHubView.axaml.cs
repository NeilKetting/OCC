using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.CalendarHub.Views
{
    public partial class CalendarHubView : UserControl
    {
        public CalendarHubView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
