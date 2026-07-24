using OCC.WpfClient.Features.ProcurementHub.ViewModels;
using System.Windows.Controls;

namespace OCC.WpfClient.Features.ProcurementHub.Views
{
    public partial class PurchaseOrderDetailView : UserControl
    {
        private bool _isLoaded;

        public PurchaseOrderDetailView()
        {
            InitializeComponent();
            Loaded += PurchaseOrderDetailView_Loaded;
        }

        private void PurchaseOrderDetailView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;

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

        private void DataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Tab && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.None)
            {
                if (sender is DataGrid dataGrid && dataGrid.Items.Count > 0)
                {
                    var lastRowIndex = dataGrid.Items.Count - 1;
                    int selectedIndex = dataGrid.SelectedIndex;

                    // If currently on the last row
                    if (selectedIndex == lastRowIndex || dataGrid.CurrentItem == dataGrid.Items[lastRowIndex])
                    {
                        var currentColumnIndex = dataGrid.CurrentCell.Column?.DisplayIndex ?? -1;
                        var totalColumns = dataGrid.Columns.Count;

                        // If focused on the last column (e.g. DEL button or UNIT PRICE / AMOUNT cell)
                        if (currentColumnIndex >= totalColumns - 2)
                        {
                            if (DataContext is PurchaseOrderDetailViewModel viewModel)
                            {
                                viewModel.AddLineCommand.Execute(null);
                            }
                        }
                    }
                }
            }
        }
    }
}
