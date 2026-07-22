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

        private void TextBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        private void TextBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                textBox.Focus();
            }
        }
    }
}
