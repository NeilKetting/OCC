using System.Windows;
using System.Windows.Input;

namespace OCC.WpfClient.Infrastructure.AttachedProperties
{
    public static class CommandProperties
    {
        public static readonly DependencyProperty LostFocusCommandProperty =
            DependencyProperty.RegisterAttached(
                "LostFocusCommand",
                typeof(ICommand),
                typeof(CommandProperties),
                new PropertyMetadata(null, OnLostFocusCommandChanged));

        public static readonly DependencyProperty DoubleClickCommandProperty =
            DependencyProperty.RegisterAttached(
                "DoubleClickCommand",
                typeof(ICommand),
                typeof(CommandProperties),
                new PropertyMetadata(null, OnDoubleClickCommandChanged));

        public static ICommand GetDoubleClickCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(DoubleClickCommandProperty);
        }

        public static void SetDoubleClickCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(DoubleClickCommandProperty, value);
        }

        public static ICommand GetLostFocusCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(LostFocusCommandProperty);
        }

        public static void SetLostFocusCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(LostFocusCommandProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.RegisterAttached(
                "CommandParameter",
                typeof(object),
                typeof(CommandProperties),
                new PropertyMetadata(null));

        public static object GetCommandParameter(DependencyObject obj)
        {
            return obj.GetValue(CommandParameterProperty);
        }

        public static void SetCommandParameter(DependencyObject obj, object value)
        {
            obj.SetValue(CommandParameterProperty, value);
        }

        public static readonly DependencyProperty SelectionChangedCommandProperty =
            DependencyProperty.RegisterAttached(
                "SelectionChangedCommand",
                typeof(ICommand),
                typeof(CommandProperties),
                new PropertyMetadata(null, OnSelectionChangedCommandChanged));

        public static ICommand GetSelectionChangedCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(SelectionChangedCommandProperty);
        }

        public static void SetSelectionChangedCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(SelectionChangedCommandProperty, value);
        }

        public static readonly DependencyProperty IsFilteredProperty =
            DependencyProperty.RegisterAttached(
                "IsFiltered",
                typeof(bool),
                typeof(CommandProperties),
                new PropertyMetadata(false, OnIsFilteredChanged));

        public static bool GetIsFiltered(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsFilteredProperty);
        }

        public static void SetIsFiltered(DependencyObject obj, bool value)
        {
            obj.SetValue(IsFilteredProperty, value);
        }

        public static readonly DependencyProperty HideArrowProperty =
            DependencyProperty.RegisterAttached("HideArrow", typeof(bool), typeof(CommandProperties), new PropertyMetadata(false));

        public static bool GetHideArrow(DependencyObject obj) => (bool)obj.GetValue(HideArrowProperty);
        public static void SetHideArrow(DependencyObject obj, bool value) => obj.SetValue(HideArrowProperty, value);

        private static void OnIsFilteredChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.ComboBox comboBox)
            {
                if ((bool)e.NewValue)
                {
                    bool isUpdating = false;
                    comboBox.IsSynchronizedWithCurrentItem = false;

                    comboBox.SelectionChanged += (s, ev) =>
                    {
                        if (comboBox.Items.CanFilter && comboBox.Items.Filter != null)
                        {
                            comboBox.Items.Filter = null;
                        }

                        comboBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                        {
                            var textBox = comboBox.Template.FindName("PART_EditableTextBox", comboBox) as System.Windows.Controls.TextBox;
                            if (textBox != null)
                            {
                                textBox.SelectionStart = textBox.Text.Length;
                                textBox.SelectionLength = 0;
                            }
                        }));
                    };

                    comboBox.DropDownClosed += (s, ev) =>
                    {
                        if (comboBox.Items.CanFilter && comboBox.Items.Filter != null)
                        {
                            comboBox.Items.Filter = null;
                        }

                        comboBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                        {
                            var textBox = comboBox.Template.FindName("PART_EditableTextBox", comboBox) as System.Windows.Controls.TextBox;
                            if (textBox != null)
                            {
                                textBox.SelectionStart = textBox.Text.Length;
                                textBox.SelectionLength = 0;
                            }
                        }));
                    };

                    comboBox.DropDownOpened += (s, ev) =>
                    {
                        if (comboBox.Items.CanFilter && comboBox.Items.Filter != null)
                        {
                            comboBox.Items.Filter = null;
                            comboBox.Items.Refresh();
                        }
                    };

                    comboBox.Loaded += (s, ev) =>
                    {
                        var textBox = comboBox.Template.FindName("PART_EditableTextBox", comboBox) as System.Windows.Controls.TextBox;
                        if (textBox != null)
                        {
                            textBox.TextChanged += (sender, args) =>
                            {
                                if (isUpdating) return;

                                if (!comboBox.IsKeyboardFocusWithin)
                                {
                                    if (comboBox.Items.CanFilter && comboBox.Items.Filter != null)
                                    {
                                        comboBox.Items.Filter = null;
                                    }
                                    return;
                                }

                                isUpdating = true;
                                try
                                {
                                    string filterText = textBox.SelectionLength > 0 
                                        ? textBox.Text.Substring(0, textBox.SelectionStart) 
                                        : textBox.Text;

                                    if (comboBox.IsKeyboardFocusWithin && comboBox.IsDropDownOpen == false && !string.IsNullOrEmpty(filterText))
                                    {
                                        comboBox.IsDropDownOpen = true;
                                    }

                                    if (comboBox.Items.CanFilter)
                                    {
                                        comboBox.Items.Filter = item =>
                                        {
                                            if (string.IsNullOrEmpty(filterText)) return true;
                                            
                                            if (item is OCC.Shared.Models.InventoryItem inv)
                                            {
                                                return inv.Sku.Contains(filterText, System.StringComparison.OrdinalIgnoreCase) ||
                                                       (inv.Description != null && inv.Description.Contains(filterText, System.StringComparison.OrdinalIgnoreCase));
                                            }
                                            if (item is OCC.Shared.Models.Supplier suppModel)
                                            {
                                                return suppModel.Name != null && suppModel.Name.Contains(filterText, System.StringComparison.OrdinalIgnoreCase);
                                            }
                                            if (item is OCC.Shared.DTOs.SupplierSummaryDto supp)
                                            {
                                                return supp.Name.Contains(filterText, System.StringComparison.OrdinalIgnoreCase);
                                            }
                                            
                                            if (!string.IsNullOrEmpty(comboBox.DisplayMemberPath))
                                            {
                                                var prop = item.GetType().GetProperty(comboBox.DisplayMemberPath);
                                                if (prop != null)
                                                {
                                                    var val = prop.GetValue(item)?.ToString();
                                                    return val != null && val.Contains(filterText, System.StringComparison.OrdinalIgnoreCase);
                                                }
                                            }
                                            
                                            return true;
                                        };
                                        comboBox.Items.Refresh();
                                    }
                                }
                                finally
                                {
                                    isUpdating = false;
                                }
                            };

                            textBox.PreviewKeyDown += (sender, args) =>
                            {
                                if (args.Key == System.Windows.Input.Key.Down || args.Key == System.Windows.Input.Key.Up)
                                {
                                    if (!comboBox.IsDropDownOpen) comboBox.IsDropDownOpen = true;

                                    if (args.Key == System.Windows.Input.Key.Down) comboBox.Items.MoveCurrentToNext();
                                    else comboBox.Items.MoveCurrentToPrevious();

                                    if (comboBox.Items.IsCurrentAfterLast) comboBox.Items.MoveCurrentToLast();
                                    if (comboBox.Items.IsCurrentBeforeFirst) comboBox.Items.MoveCurrentToFirst();

                                    if (comboBox.Items.CurrentItem != null)
                                    {
                                        isUpdating = true;
                                        comboBox.SelectedItem = comboBox.Items.CurrentItem;
                                        isUpdating = false;
                                    }

                                    args.Handled = true;
                                }
                                else if (args.Key == System.Windows.Input.Key.Enter)
                                {
                                    if (comboBox.Items.CurrentItem != null)
                                    {
                                        isUpdating = true;
                                        comboBox.SelectedItem = comboBox.Items.CurrentItem;
                                        isUpdating = false;
                                        comboBox.IsDropDownOpen = false;
                                        args.Handled = true;

                                        var command = GetSelectionChangedCommand(comboBox);
                                        if (command != null)
                                        {
                                            var param = GetCommandParameter(comboBox);
                                            if (command.CanExecute(param)) command.Execute(param);
                                        }
                                    }
                                }
                            };
                        }
                    };

                    comboBox.LostFocus += (s, ev) =>
                    {
                        if (comboBox.Items.CanFilter && comboBox.Items.Filter != null)
                        {
                            comboBox.Items.Filter = null;
                        }

                        var command = GetLostFocusCommand(comboBox);
                        if (command != null)
                        {
                            var param = GetCommandParameter(comboBox);
                            if (command.CanExecute(param)) command.Execute(param);
                        }
                    };

                    comboBox.SelectionChanged += (s, ev) =>
                    {
                        if (comboBox.IsDropDownOpen == false)
                        {
                            if (comboBox.Items.CanFilter && comboBox.Items.Filter != null)
                            {
                                comboBox.Items.Filter = null;
                            }

                            var command = GetSelectionChangedCommand(comboBox);
                            if (command != null)
                            {
                                var param = GetCommandParameter(comboBox);
                                if (command.CanExecute(param)) command.Execute(param);
                            }
                        }
                    };
                }
            }
        }

        private static void OnSelectionChangedCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.Primitives.Selector selector)
            {
                selector.SelectionChanged -= OnSelectorSelectionChanged;
                if (e.NewValue != null)
                {
                    selector.SelectionChanged += OnSelectorSelectionChanged;
                }
            }
        }

        private static void OnSelectorSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is DependencyObject d)
            {
                var command = GetSelectionChangedCommand(d);
                var parameter = GetCommandParameter(d);

                if (command != null && command.CanExecute(parameter))
                {
                    command.Execute(parameter);
                }
            }
        }

        private static void OnLostFocusCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                element.LostFocus -= OnElementLostFocus;
                if (e.NewValue != null)
                {
                    element.LostFocus += OnElementLostFocus;
                }
            }
        }

        private static void OnElementLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject d)
            {
                var command = GetLostFocusCommand(d);
                var parameter = GetCommandParameter(d);

                if (command != null && command.CanExecute(parameter))
                {
                    command.Execute(parameter);
                }
            }
        }

        private static void OnDoubleClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.Control control)
            {
                control.MouseDoubleClick -= OnControlMouseDoubleClick;
                if (e.NewValue != null)
                {
                    control.MouseDoubleClick += OnControlMouseDoubleClick;
                }
            }
        }

        private static void OnControlMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DependencyObject d)
            {
                var command = GetDoubleClickCommand(d);
                var parameter = GetCommandParameter(d);

                // For DataGridRow, we often want the row's data as the parameter if no explicit parameter is set
                if (parameter == null && d is System.Windows.Controls.DataGridRow row)
                {
                    parameter = row.DataContext;
                }

                if (command != null && command.CanExecute(parameter))
                {
                    command.Execute(parameter);
                }
            }
        }
    }
}
