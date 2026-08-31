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
                if (viewModel.CurrentOrder == null && viewModel.LoadDataCommand.CanExecute(null))
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

        private void CustomProjectInputBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is PurchaseOrderDetailViewModel viewModel)
            {
                viewModel.LoadCustomProjectHistory();
            }
        }

        private void CustomProjectInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is PurchaseOrderDetailViewModel viewModel)
            {
                if (viewModel.IsOtherProjectSelected && CustomProjectInputBox.IsKeyboardFocusWithin)
                {
                    viewModel.LoadCustomProjectHistory();
                    viewModel.IsCustomProjectSuggestionsOpen = viewModel.CustomProjectSuggestions.Count > 0;
                }
            }
        }

        private void CustomProjectInputBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is PurchaseOrderDetailViewModel viewModel)
            {
                viewModel.AddCurrentCustomProjectToHistory();
            }
        }

        private void CustomProjectItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement element && element.DataContext is string selectedName && DataContext is PurchaseOrderDetailViewModel viewModel)
            {
                if (viewModel.CurrentOrder != null)
                {
                    viewModel.CurrentOrder.ProjectName = selectedName;
                }
                viewModel.IsCustomProjectSuggestionsOpen = false;
            }
        }

        private void ScopeOfWorkInputBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is PurchaseOrderDetailViewModel viewModel)
            {
                viewModel.LoadScopeOfWorkHistory();
            }
        }

        private void ScopeOfWorkInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is PurchaseOrderDetailViewModel viewModel)
            {
                if (ScopeOfWorkInputBox.IsKeyboardFocusWithin)
                {
                    viewModel.LoadScopeOfWorkHistory();
                    viewModel.IsScopeOfWorkSuggestionsOpen = viewModel.ScopeOfWorkSuggestions.Count > 0;
                }
            }
        }

        private void ScopeOfWorkInputBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is PurchaseOrderDetailViewModel viewModel)
            {
                viewModel.AddCurrentScopeOfWorkToHistory();
            }
        }

        private void ScopeOfWorkItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement element && element.DataContext is string selectedScope && DataContext is PurchaseOrderDetailViewModel viewModel)
            {
                if (viewModel.CurrentOrder != null)
                {
                    viewModel.CurrentOrder.ScopeOfWork = selectedScope;
                }
                viewModel.IsScopeOfWorkSuggestionsOpen = false;
            }
        }

        private System.Windows.Threading.DispatcherTimer? _emailPopupTimer;

        private void EmailToolbarButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _emailPopupTimer?.Stop();
            EmailContactPopup.IsOpen = true;
        }

        private void EmailToolbarButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            StartEmailPopupTimer();
        }

        private void EmailCardBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _emailPopupTimer?.Stop();
            EmailContactPopup.IsOpen = true;
        }

        private void EmailCardBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            StartEmailPopupTimer();
        }

        private void EditContactButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _emailPopupTimer?.Stop();
            EmailContactPopup.IsOpen = false;
        }

        private void StartEmailPopupTimer()
        {
            _emailPopupTimer?.Stop();
            _emailPopupTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(300)
            };
            _emailPopupTimer.Tick += (s, args) =>
            {
                _emailPopupTimer.Stop();
                if (!EmailToolbarButton.IsMouseOver && !EmailCardBorder.IsMouseOver)
                {
                    EmailContactPopup.IsOpen = false;
                }
            };
            _emailPopupTimer.Start();
        }
    }
}
