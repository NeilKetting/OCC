using System.Windows;
using System.Windows.Controls;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Infrastructure
{
    public class BaseListView : UserControl
    {
        public BaseListView()
        {
            this.Loaded += BaseListView_Loaded;
        }

        private void BaseListView_Loaded(object sender, RoutedEventArgs e)
        {
            // Find the DataGrid and hook into ColumnReordered if we want to standardize layout saving
            var dataGrid = FindVisualChild<DataGrid>(this);
            if (dataGrid != null)
            {
                dataGrid.ColumnReordered += DataGrid_ColumnReordered;
            }
        }

        private void DataGrid_ColumnReordered(object sender, DataGridColumnEventArgs e)
        {
            // Standard pattern for saving layout
            if (DataContext != null)
            {
                try
                {
                    dynamic vm = DataContext;
                    vm.SaveLayoutCommand?.Execute(null);
                }
                catch
                {
                    // Command might not exist on all ViewModels yet
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }
    }
}
