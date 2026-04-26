using OCC.Client.Features.OrdersHub.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Avalonia;
using System;

namespace OCC.Client.Features.OrdersHub.Views
{
    public partial class CreateOrderView : UserControl
    {
        public CreateOrderView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Force close any open dropdowns when navigating away
            if (this.DataContext is CreateOrderViewModel vm)
            {
                // We can't easily iterate the virtualized DataGrid rows, 
                // but we can ensure the ViewModel knows to reset any state if needed.
                // However, for the AutoCompleteBox popup, it should close if the control is detached.
                // If it's "Stuck", it might be because of a bug in Avalonia where Popups don't close on detach.

                // Try to find any open popups in the visual tree (if possible) or just rely on the fact that
                // if we change IsDropDownOpen = false on the focused element it might help.

                var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
                if (focusManager?.GetFocusedElement() is AutoCompleteBox box)
                {
                    box.IsDropDownOpen = false;
                }
            }
        }

        private void SkuBox_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                // Force validation on Enter
                if (DataContext is CreateOrderViewModel vm && sender is ComboBox box)
                {
                    var searchText = box.SelectedItem?.ToString();
                    if (!string.IsNullOrWhiteSpace(searchText))
                    {
                        vm.ValidateItemSearchCommand.Execute(searchText);
                    }
                }
            }
        }



        private void ProductBox_KeyUp(object? sender, KeyEventArgs e)
        {
            if (DataContext is CreateOrderViewModel vm && sender is AutoCompleteBox box)
            {
                var text = box.Text ?? string.Empty;

                // Set custom filter if not already set
                if (box.ItemFilter == null)
                {
                    box.ItemFilter = (search, item) =>
                    {
                        if (string.IsNullOrWhiteSpace(search)) return true;
                        if (item is OCC.Shared.Models.InventoryItem invItem)
                        {
                            return (invItem.Sku?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                   (invItem.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
                        }
                        return false;
                    };
                }

                // Standard Enter key validation
                if (e.Key == Key.Enter || e.Key == Key.Return)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        vm.ValidateItemSearchCommand.Execute(text);
                    }
                }
            }
        }

        public void GridProductBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is AutoCompleteBox box &&
                box.DataContext is OCC.Client.ModelWrappers.OrderLineWrapper line &&
                box.SelectedItem is OCC.Shared.Models.InventoryItem item &&
                this.DataContext is CreateOrderViewModel vm)
            {
                vm.Lines.UpdateLineFromSelection(line, item);
                box.IsDropDownOpen = false;
            }
        }

        public void GridProductBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is AutoCompleteBox box &&
                box.DataContext is OCC.Client.ModelWrappers.OrderLineWrapper line &&
                this.DataContext is CreateOrderViewModel vm)
            {
                vm.Lines.TryCommitAutoSelection(line);
            }
        }

        public void UOM_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Close the flyout when an item is selected
            if (sender is ListBox listBox && listBox.IsVisible)
            {
                // Find the parent FlyoutPresenter to close the Popup
                // Or simpler: Just unselect if needed, but we want to close the popup.
                // In Avalonia, the Flyout doesn't have a direct "Close" on the content.
                // But we can find the popup?
                // Actually, standard behavior for ListBox in Flyout doesn't auto-close.
                // We can use the attached property or search visual tree.

                // Robust way: Find the Popup and close it.
                var popup = listBox.FindAncestorOfType<Avalonia.Controls.Primitives.Popup>();
                if (popup != null)
                {
                    popup.IsOpen = false;
                }
            }
        }
        private void AutoCompleteBox_GotFocus(object? sender, RoutedEventArgs e)
        {
            // Logic removed to restore dropdown functionality
        }

        public void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectAll();
            }
            else if (sender is NumericUpDown nud)
            {
                // nud.SelectAll(); // Not available in this version
            }
        }
    }
}
