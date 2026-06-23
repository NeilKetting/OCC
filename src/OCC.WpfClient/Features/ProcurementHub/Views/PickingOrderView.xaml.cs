using OCC.WpfClient.Features.ProcurementHub.ViewModels;
using System.Windows.Controls;

namespace OCC.WpfClient.Features.ProcurementHub.Views
{
    public partial class PickingOrderView : UserControl
    {
        public PickingOrderView()
        {
            InitializeComponent();
            Loaded += PickingOrderView_Loaded;
        }

        private void PickingOrderView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is PickingOrderViewModel viewModel)
            {
                if (viewModel.LoadDataCommand.CanExecute(null))
                {
                    viewModel.LoadDataCommand.Execute(null);
                }
            }
        }
    }
}
