using OCC.Client.Features.TimeAttendanceHub.ViewModels;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.TimeAttendanceHub.Views
{
    public partial class DailyTimesheetView : UserControl
    {
        public DailyTimesheetView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
