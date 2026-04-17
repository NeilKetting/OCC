using System.Windows.Controls;
using OCC.WpfClient.Features.EmployeeHub.ViewModels;

namespace OCC.WpfClient.Features.EmployeeHub.Views
{
    public partial class EmployeeListView : UserControl
    {
        public EmployeeListView()
        {
            InitializeComponent();
        }
        public void DataGrid_ColumnReordered(object sender, DataGridColumnEventArgs e)
        {
            if (DataContext is ViewModels.EmployeeListViewModel vm)
            {
                vm.SaveLayoutCommand.Execute(null);
            }
        }
    }
}
