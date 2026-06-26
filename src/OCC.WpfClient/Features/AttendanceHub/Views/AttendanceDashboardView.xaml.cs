using OCC.WpfClient.Infrastructure;
using System.Windows.Controls;

namespace OCC.WpfClient.Features.AttendanceHub.Views
{
    public class AttendanceDashboardViewBase : ListViewBase { }

    public partial class AttendanceDashboardView : AttendanceDashboardViewBase
    {
        public AttendanceDashboardView()
        {
            InitializeComponent();
        }
    }
}
