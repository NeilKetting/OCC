using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using OCC.WpfClient.Infrastructure;

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

        public BaseListView()
        {
            this.Loaded += BaseListView_Loaded;
        }

        private void BaseListView_Loaded(object sender, RoutedEventArgs e)
        {
            var dataGrid = FindVisualChild<DataGrid>(this);
            if (dataGrid != null)
            {
                dataGrid.ColumnReordered += DataGrid_ColumnReordered;
            }
        }

        public static readonly DependencyProperty IsDrawer2OpenProperty =
            DependencyProperty.Register("IsDrawer2Open", typeof(bool), typeof(BaseListView), 
                new PropertyMetadata(false, OnIsDrawer2OpenChanged));

        public bool IsDrawer2Open
        {
            get => (bool)GetValue(IsDrawer2OpenProperty);
            set => SetValue(IsDrawer2OpenProperty, value);
        }

        private static void OnIsDrawerOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseListView view)
            {
                view.HandleDrawerTransition((bool)e.NewValue);
            }
        }

        private static void OnIsDrawer2OpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseListView view)
            {
                view.HandleDrawerTransition((bool)e.NewValue);
            }
        }

        private void HandleDrawerTransition(bool isOpen)
        {
            // Try to find the drawer overlay grid. 
            // We expect a Grid named "DrawerOverlay" or just the first Grid with Grid.RowSpan > 1
            var overlay = this.FindName("DrawerOverlay") as Grid ?? FindVisualChild<Grid>(this, g => Grid.GetRowSpan(g) > 1);
            if (overlay == null) return;

            if (isOpen)
            {
                overlay.Visibility = Visibility.Visible;
                var sb = this.Resources["OpenDrawer"] as Storyboard;
                sb?.Begin(this);
            }
            else
            {
                var sb = this.Resources["CloseDrawer"] as Storyboard;
                if (sb != null)
                {
                    sb = sb.Clone(); // Clone to avoid sharing issues
                    sb.Completed += (s, args) =>
                    {
                        if (!IsDrawerOpen) // Re-check in case it was opened during animation
                            overlay.Visibility = Visibility.Collapsed;
                    };
                    sb.Begin(this);
                }
                else
                {
                    overlay.Visibility = Visibility.Collapsed;
                }
            }
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
    }
}
