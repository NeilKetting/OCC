using OCC.WpfClient.Infrastructure;
using System.Windows.Controls;

namespace OCC.WpfClient.Features.AttendanceHub.Views
{
    public class LoansManagementListViewBase : ListViewBase { }

    public partial class LoansManagementView : LoansManagementListViewBase
    {
        public LoansManagementView()
        {
            InitializeComponent();
        }
    }
}
