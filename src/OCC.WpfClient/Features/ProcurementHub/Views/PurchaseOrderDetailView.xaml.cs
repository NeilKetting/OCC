using OCC.WpfClient.Features.ProcurementHub.ViewModels;
using System.Windows.Controls;

namespace OCC.WpfClient.Features.ProcurementHub.Views
{
    public partial class PurchaseOrderDetailView : UserControl
    {
        public PurchaseOrderDetailView()
        {
            InitializeComponent();
            Loaded += PurchaseOrderDetailView_Loaded;
        }

        private void PurchaseOrderDetailView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is PurchaseOrderDetailViewModel viewModel)
            {
                if (viewModel.LoadDataCommand.CanExecute(null))
                {
                    viewModel.LoadDataCommand.Execute(null);
                }
            }
        }
    }
}
