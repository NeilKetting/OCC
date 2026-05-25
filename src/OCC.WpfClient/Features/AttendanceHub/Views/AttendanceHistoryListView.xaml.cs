using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Features.AttendanceHub.Views
{
    /// <summary>
    /// Base class alias so XAML can reference ListViewBase cleanly.
    /// </summary>
    public class AttendanceHistoryListViewBase : ListViewBase { }

    public partial class AttendanceHistoryListView : AttendanceHistoryListViewBase
    {
        public AttendanceHistoryListView()
        {
            InitializeComponent();
        }
    }
}
