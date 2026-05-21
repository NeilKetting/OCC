using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Converters;

namespace OCC.WpfClient.Infrastructure
{
    public class BaseListView : UserControl
    {
        public static readonly DependencyProperty IsDrawerOpenProperty =
            DependencyProperty.Register("IsDrawerOpen", typeof(bool), typeof(BaseListView), 
                new PropertyMetadata(false, OnIsDrawerOpenChanged));

        public bool IsDrawerOpen
        {
            get => (bool)GetValue(IsDrawerOpenProperty);
            set => SetValue(IsDrawerOpenProperty, value);
        }

        public static readonly DependencyProperty DrawerWidthProperty =
            DependencyProperty.Register("DrawerWidth", typeof(double), typeof(BaseListView), 
                new PropertyMetadata(550.0));

        public double DrawerWidth
        {
            get => (double)GetValue(DrawerWidthProperty);
            set => SetValue(DrawerWidthProperty, value);
        }

        public static readonly DependencyProperty OverlayContentProperty =
            DependencyProperty.Register("OverlayContent", typeof(object), typeof(BaseListView), 
                new PropertyMetadata(null));

        public object OverlayContent
        {
            get => GetValue(OverlayContentProperty);
            set => SetValue(OverlayContentProperty, value);
        }

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register("SelectedItems", typeof(System.Collections.IList), typeof(BaseListView), 
                new PropertyMetadata(null));

        public System.Collections.IList SelectedItems
        {
            get => (System.Collections.IList)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        static BaseListView()
        {
            // Register class handlers for DataGridRow to intercept right-clicks before selection logic
            EventManager.RegisterClassHandler(typeof(DataGridRow), UIElement.PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(OnDataGridRowPreviewRightMouseDown), true);
        }

        private static void OnDataGridRowPreviewRightMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
            {
                var dg = FindVisualParentStatic<DataGrid>(row);
                if (dg != null)
                {
                    var listView = FindVisualParentStatic<BaseListView>(dg);
                    if (listView != null)
                    {
                        listView.DataGrid_PreviewMouseRightButtonDown(dg, e);
                    }
                }
            }
        }

        public BaseListView()
        {
            this.Loaded += BaseListView_Loaded;
        }

        private void BaseListView_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure all DataGrids have the selection column and correct mode
            var dataGrids = FindVisualChildren<DataGrid>(this);
            foreach (var dataGrid in dataGrids)
            {
                dataGrid.SelectionMode = DataGridSelectionMode.Extended;
                AddSelectionColumn(dataGrid);

                dataGrid.IsReadOnly = false;
                foreach (var col in dataGrid.Columns)
                {
                    if (col.Header?.ToString() != "" && col.Header?.ToString() != "ACTIONS")
                    {
                        col.IsReadOnly = true;
                    }
                }

                InjectDefaultContextMenu(dataGrid);
                dataGrid.SelectionChanged += DataGrid_SelectionChanged;
                dataGrid.ContextMenuOpening += DataGrid_ContextMenuOpening;
            }

            if (_drawerOverlay == null)
            {
                InjectDrawerOverlay();
            }
        }

        private void InjectDefaultContextMenu(DataGrid dg)
        {
            var menu = new ContextMenu();
            
            // Open Item
            var openItem = new MenuItem { Header = "Open" };
            var openIcon = new TextBlock { Text = "", FontSize = 14 };
            openIcon.SetResourceReference(TextBlock.StyleProperty, "SymbolIcon");
            openIcon.Foreground = (Brush)Application.Current.Resources["SuccessGreen"];
            openItem.Icon = openIcon;
            openItem.SetBinding(MenuItem.CommandProperty, new Binding("PlacementTarget.Tag.OpenCommand") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ContextMenu), 1) });
            openItem.SetBinding(MenuItem.CommandParameterProperty, new Binding("."));
            menu.Items.Add(openItem);

            // Edit Item
            var editItem = new MenuItem { Header = "Edit" };
            var editIcon = new TextBlock { Text = "", FontSize = 14 };
            editIcon.SetResourceReference(TextBlock.StyleProperty, "SymbolIcon");
            editIcon.Foreground = (Brush)Application.Current.Resources["AccentBlue"];
            editItem.Icon = editIcon;
            editItem.SetBinding(MenuItem.CommandProperty, new Binding("PlacementTarget.Tag.EditCommand") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ContextMenu), 1) });
            editItem.SetBinding(MenuItem.CommandParameterProperty, new Binding("."));
            menu.Items.Add(editItem);

            menu.Items.Add(new Separator());

            // Delete Item
            var deleteItem = new MenuItem { Header = "Delete" };
            var deleteIcon = new TextBlock { Text = "", FontSize = 14 };
            deleteIcon.SetResourceReference(TextBlock.StyleProperty, "SymbolIcon");
            deleteIcon.Foreground = (Brush)Application.Current.Resources["ErrorRed"];
            deleteItem.Icon = deleteIcon;
            deleteItem.SetBinding(MenuItem.CommandProperty, new Binding("PlacementTarget.Tag.DeleteCommand") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ContextMenu), 1) });
            deleteItem.SetBinding(MenuItem.CommandParameterProperty, new Binding("."));
            menu.Items.Add(deleteItem);

            // Create or Update RowStyle
            var style = new Style(typeof(DataGridRow), dg.RowStyle ?? (Style)Application.Current.Resources[typeof(DataGridRow)]);
            
            // Ensure the Tag is bound to the DataGrid's DataContext (the ViewModel) 
            // so the ContextMenu can reach it via PlacementTarget.Tag
            style.Setters.Add(new Setter(FrameworkElement.TagProperty, new Binding("DataContext") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1) }));
            
            // Check if ContextMenu is already set in the style
            bool hasMenu = false;
            foreach (Setter setter in style.Setters)
            {
                if (setter.Property == FrameworkElement.ContextMenuProperty)
                {
                    hasMenu = true;
                    break;
                }
            }

            if (!hasMenu)
            {
                style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, menu));
                dg.RowStyle = style;
            }
        }

        private void AddSelectionColumn(DataGrid dg)
        {
            if (dg.Columns.Any(c => c.Header?.ToString() == " " || c.GetValue(FrameworkElement.NameProperty)?.ToString() == "SelectionColumn")) return;

            // Cell Template
            var template = new DataTemplate();
            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
            gridFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            gridFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);

            var factory = new FrameworkElementFactory(typeof(CheckBox));
            factory.SetResourceReference(FrameworkElement.StyleProperty, "ModernCheckBox");
            factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.SetValue(UIElement.FocusableProperty, false);
            factory.SetValue(UIElement.IsHitTestVisibleProperty, false); // Let clicks pass through to the Grid
            
            factory.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsSelected") 
            { 
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            
            gridFactory.AppendChild(factory);
            
            // Handle clicks on the Grid instead of the CheckBox to avoid internal control logic
            gridFactory.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnCheckBoxPreviewMouseDown), true);
            
            template.VisualTree = gridFactory;

            // Header Template (Select All)
            var headerTemplate = new DataTemplate();
            var headerFactory = new FrameworkElementFactory(typeof(CheckBox));
            headerFactory.SetResourceReference(FrameworkElement.StyleProperty, "ModernCheckBox");
            headerFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            headerFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            // We can't easily bind "Select All" to the DataGrid selection state without a helper or behavior,
            // but we can at least handle the click to select/deselect all.
            headerFactory.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnSelectAllPreviewMouseDown), true);
            headerTemplate.VisualTree = headerFactory;

            var col = new DataGridTemplateColumn
            {
                Header = " ",
                HeaderTemplate = headerTemplate,
                Width = DataGridLength.Auto,
                CellTemplate = template,
                CanUserSort = false,
                CanUserReorder = false,
                IsReadOnly = false
            };
            dg.Columns.Insert(0, col);
        }

        private void OnCheckBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // This handler is now on the Grid parent of the CheckBox.
            // We only care about left clicks to toggle selection.
            if (e.ChangedButton == MouseButton.Left)
            {
                var row = FindVisualParent<DataGridRow>(sender as DependencyObject);
                if (row != null)
                {
                    row.IsSelected = !row.IsSelected;
                    e.Handled = true;
                }
            }
        }

        private void OnSelectAllPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            if (sender is CheckBox cb)
            {
                bool newState = !cb.IsChecked == true;
                cb.IsChecked = newState;
                e.Handled = true;

                // Find the DataGrid
                var dg = FindVisualChild<DataGrid>(this);
                if (dg != null)
                {
                    if (newState)
                    {
                        dg.SelectAll();
                    }
                    else
                    {
                        dg.UnselectAll();
                    }
                }
            }
        }

        private void DataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid dg)
            {
                var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
                if (row != null)
                {
                    // If already selected, we let it be so multi-selection works for context menus.
                    // But we still need to update the menu items!
                    if (row.IsSelected)
                    {
                        var rowMenu = row.ContextMenu ?? dg.ContextMenu;
                        if (rowMenu != null)
                        {
                            UpdateContextMenu(rowMenu, dg.SelectedItems.Count > 1, dg.SelectedItems);
                        }
                        return;
                    }

                    // If NOT selected, we show the menu for this row but STOP the selection event.
                    var menu = row.ContextMenu ?? dg.ContextMenu;
                    
                    // If still no menu, try to find the default one we might have injected
                    if (menu == null && row.Style != null)
                    {
                        // Note: row.ContextMenu should usually return the one from the style if set.
                    }

                    if (menu != null)
                    {
                        row.Focus();
                        UpdateContextMenu(menu, dg.SelectedItems.Count > 1, dg.SelectedItems);
                        menu.PlacementTarget = row;
                        menu.IsOpen = true;
                        e.Handled = true; // Prevents DataGrid from selecting the row
                    }
                    else
                    {
                        // Even if no menu, stop the right-click from causing selection
                        e.Handled = true;
                    }
                }
            }
        }

        private void DataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is DataGrid dg)
            {
                var isMulti = dg.SelectedItems.Count > 1;
                
                // 1. Check DataGrid itself
                if (dg.ContextMenu != null) 
                {
                    UpdateContextMenu(dg.ContextMenu, isMulti, dg.SelectedItems);
                }

                // 2. Check the specific element that was clicked (usually a row or cell)
                // We traverse up the visual tree to find if any parent has a ContextMenu
                DependencyObject? obj = e.OriginalSource as DependencyObject;
                while (obj != null && obj != dg)
                {
                    if (obj is FrameworkElement fe && fe.ContextMenu != null)
                    {
                        UpdateContextMenu(fe.ContextMenu, isMulti, dg.SelectedItems);
                        break;
                    }
                    obj = VisualTreeHelper.GetParent(obj);
                }
            }
        }

        private void UpdateContextMenu(ContextMenu menu, bool isMultiSelect, System.Collections.IList selectedItems)
        {
            foreach (var item in menu.Items)
            {
                if (item is MenuItem mi)
                {
                    var header = mi.Header?.ToString()?.ToLower() ?? "";
                    
                    if (isMultiSelect)
                    {
                        bool isDeleteAction = header.Contains("delete") || header.Contains("archive") || header.Contains("remove");
                        
                        if (isDeleteAction)
                        {
                            mi.IsEnabled = true;
                            mi.CommandParameter = selectedItems;
                        }
                        else
                        {
                            mi.IsEnabled = false;
                        }
                    }
                    else
                    {
                        mi.IsEnabled = true;
                        // Reset CommandParameter to the default (which is usually the row item)
                        // but since MenuItem doesn't have a way to reset to binding easily without re-applying, 
                        // we just hope the binding takes over or we set it back to the first item if needed.
                        // Actually, the binding is on PlacementTarget which is the row.
                        mi.CommandParameter = null; 
                        mi.SetBinding(MenuItem.CommandParameterProperty, new Binding("."));
                    }
                }
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dg)
            {
                SelectedItems = dg.SelectedItems;
            }
        }

        private static void OnIsDrawerOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseListView view)
            {
                view.HandleDrawerTransition((bool)e.NewValue);
            }
        }

        private Grid? _drawerOverlay;
        private Storyboard? _openStoryboard;
        private Storyboard? _closeStoryboard;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _drawerOverlay = GetTemplateChild("DrawerOverlay") as Grid;

            if (_drawerOverlay != null)
            {
                var openSb = _drawerOverlay.Resources["OpenDrawer"] as Storyboard ?? TryFindResource("OpenDrawer") as Storyboard;
                var closeSb = _drawerOverlay.Resources["CloseDrawer"] as Storyboard ?? TryFindResource("CloseDrawer") as Storyboard;

                var drawer = GetTemplateChild("DrawerContent") as FrameworkElement;
                var dimmer = GetTemplateChild("Dimmer") as FrameworkElement;

                if (openSb != null && drawer != null && dimmer != null)
                {
                    _openStoryboard = openSb.Clone();
                    // Set targets directly to avoid name resolution issues
                    if (_openStoryboard.Children.Count >= 2)
                    {
                        Storyboard.SetTarget(_openStoryboard.Children[0], drawer);
                        Storyboard.SetTarget(_openStoryboard.Children[1], dimmer);
                    }
                }

                if (closeSb != null && drawer != null && dimmer != null)
                {
                    _closeStoryboard = closeSb.Clone();
                    if (_closeStoryboard.Children.Count >= 2)
                    {
                        Storyboard.SetTarget(_closeStoryboard.Children[0], drawer);
                        Storyboard.SetTarget(_closeStoryboard.Children[1], dimmer);
                    }
                }
            }
        }

        private void HandleDrawerTransition(bool isOpen)
        {
            // If template isn't applied yet, we might not have the overlay. 
            // In WPF, DependencyProperties can change before OnApplyTemplate.
            // If it happens, we apply template forcefully.
            if (_drawerOverlay == null)
            {
                InjectDrawerOverlay();
            }

            if (_drawerOverlay == null) return;

            if (isOpen)
            {
                _drawerOverlay.Visibility = Visibility.Visible;
                if (_openStoryboard != null)
                {
                    _openStoryboard.Begin(); 
                }
            }
            else
            {
                if (_closeStoryboard != null)
                {
                    var sb = _closeStoryboard.Clone();
                    sb.Completed += (s, args) =>
                    {
                        if (!IsDrawerOpen)
                            _drawerOverlay.Visibility = Visibility.Collapsed;
                    };
                    sb.Begin();
                }
                else
                {
                    _drawerOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void InjectDrawerOverlay()
        {
            if (this.Content is not Panel rootPanel) return;

            // Check if already injected or manually defined
            if (rootPanel.FindName("DrawerOverlay") is Grid existingDrawer)
            {
                _drawerOverlay = existingDrawer;
                _openStoryboard = _drawerOverlay.Resources["OpenDrawer"] as Storyboard ?? this.TryFindResource("OpenDrawer") as Storyboard;
                _closeStoryboard = _drawerOverlay.Resources["CloseDrawer"] as Storyboard ?? this.TryFindResource("CloseDrawer") as Storyboard;
                return;
            }

            var grid = new Grid { Name = "DrawerOverlay", Visibility = Visibility.Collapsed };
            if (rootPanel is Grid rg && rg.RowDefinitions.Count > 0)
            {
                Grid.SetRowSpan(grid, Math.Max(1, rg.RowDefinitions.Count + 10));
            }
            
            var dimmer = new Border { Name = "BackgroundDimmer", Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)), Opacity = 0 };
            var mouseBinding = new MouseBinding { MouseAction = MouseAction.LeftClick };
            BindingOperations.SetBinding(mouseBinding, InputBinding.CommandProperty, new Binding("CloseOverlayCommand"));
            dimmer.InputBindings.Add(mouseBinding);
            grid.Children.Add(dimmer);
            
            var drawer = new Border 
            { 
                Name = "DrawerContent", 
                HorizontalAlignment = HorizontalAlignment.Right, 
                BorderThickness = new Thickness(1, 0, 0, 0)
            };
            drawer.SetBinding(FrameworkElement.WidthProperty, new Binding("DrawerWidth") { Source = this });
            drawer.SetResourceReference(Border.BackgroundProperty, "BackgroundDark");
            drawer.SetResourceReference(Border.BorderBrushProperty, "GlassBorder");
            drawer.RenderTransform = new TranslateTransform { X = DrawerWidth };
            
            var contentControl = new ContentControl();
            contentControl.SetBinding(ContentControl.ContentProperty, new Binding("OverlayContent") { Source = this });
            drawer.Child = contentControl;
            grid.Children.Add(drawer);
            
            rootPanel.Children.Add(grid);
            _drawerOverlay = grid;

            _openStoryboard = new Storyboard();
            var slideIn = new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(0.6), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(slideIn, drawer);
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            var fadeIn = new DoubleAnimation { To = 1, Duration = TimeSpan.FromSeconds(0.3) };
            Storyboard.SetTarget(fadeIn, dimmer);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
            _openStoryboard.Children.Add(slideIn);
            _openStoryboard.Children.Add(fadeIn);

            _closeStoryboard = new Storyboard();
            var slideOut = new DoubleAnimation { Duration = TimeSpan.FromSeconds(0.4), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            BindingOperations.SetBinding(slideOut, DoubleAnimation.ToProperty, new Binding("DrawerWidth") { Source = this });
            Storyboard.SetTarget(slideOut, drawer);
            Storyboard.SetTargetProperty(slideOut, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            var fadeOut = new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(0.3) };
            Storyboard.SetTarget(fadeOut, dimmer);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
            _closeStoryboard.Children.Add(slideOut);
            _closeStoryboard.Children.Add(fadeOut);
        }


        private void DataGrid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
        {
            if (DataContext != null)
            {
                try
                {
                    dynamic vm = DataContext;
                    vm.SaveLayoutCommand?.Execute(null);
                }
                catch { }
            }
        }

        protected List<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
        {
            List<T> list = new List<T>();
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T t)
                    {
                        list.Add(t);
                    }

                    List<T> childItems = FindVisualChildren<T>(child);
                    if (childItems != null && childItems.Count > 0)
                    {
                        foreach (T item in childItems)
                        {
                            list.Add(item);
                        }
                    }
                }
            }
            return list;
        }

        protected T? FindVisualChild<T>(DependencyObject? obj, Func<T, bool>? filter = null) where T : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T tChild)
                {
                    if (filter == null || filter(tChild))
                        return tChild;
                }
                
                T? childOfChild = FindVisualChild<T>(child, filter);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        protected static T? FindVisualParentStatic<T>(DependencyObject? child) where T : DependencyObject
        {
            DependencyObject? parentObject = child;
            while (parentObject != null)
            {
                parentObject = VisualTreeHelper.GetParent(parentObject);
                if (parentObject is T parent)
                    return parent;
            }
            return null;
        }

        protected T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            return FindVisualParentStatic<T>(child);
        }
    }
}
