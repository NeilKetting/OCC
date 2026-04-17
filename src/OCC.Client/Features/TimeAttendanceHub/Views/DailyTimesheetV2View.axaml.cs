using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.TimeAttendanceHub.Views
{
    public partial class DailyTimesheetV2View : UserControl
    {
        public DailyTimesheetV2View()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

