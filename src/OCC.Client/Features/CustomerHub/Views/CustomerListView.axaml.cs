using Avalonia.Controls;
using Avalonia.Input;
using OCC.Client.Features.CustomerHub.ViewModels;

namespace OCC.Client.Features.CustomerHub.Views
{
    public partial class CustomerListView : UserControl
    {
        public CustomerListView()
        {
            InitializeComponent();
        }

        private void DataGrid_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is OCC.Shared.DTOs.CustomerSummaryDto customer && 
                DataContext is CustomerManagementViewModel vm)
            {
                vm.EditCustomerCommand.Execute(customer);
            }
        }
    }
}
