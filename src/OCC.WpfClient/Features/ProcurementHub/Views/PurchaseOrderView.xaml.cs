using OCC.WpfClient.Features.ProcurementHub.ViewModels;
using System.Windows.Controls;

namespace OCC.WpfClient.Features.ProcurementHub.Views
{
    public partial class PurchaseOrderView : UserControl
    {
        public PurchaseOrderView()
        {
            InitializeComponent();
            Loaded += PurchaseOrderView_Loaded;
        }

        private void PurchaseOrderView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is PurchaseOrderViewModel viewModel)
            {
                if (viewModel.LoadDataCommand.CanExecute(null))
                {
                    viewModel.LoadDataCommand.Execute(null);
                }
            }
        }
    }
}
