using Avalonia.Controls;
using OCC.Client.Features.EmployeeHub.ViewModels;
using OCC.Client.Features.TimeAttendanceHub.ViewModels;
using OCC.Shared.DTOs;

using OCC.Client.Features.TimeAttendanceHub.Views;

namespace OCC.Client.Features.EmployeeHub.Views
{
    public partial class EmployeeListView : UserControl
    {
        public EmployeeListView()
        {
            InitializeComponent();
        }

        private void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (DataContext is EmployeeManagementViewModel vm && 
                sender is DataGrid dg && 
                dg.SelectedItem is EmployeeSummaryDto emp)
            {
                vm.EditEmployeeCommand.Execute(emp);
            }
        }
    }
}
