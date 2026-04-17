using System.Windows.Controls;
using System.Windows.Threading;

namespace OCC.WpfClient.Features.CustomerHub.Views
{
    public partial class CustomerDetailView : UserControl
    {
        public CustomerDetailView()
        {
            InitializeComponent();
            this.DataContextChanged += CustomerDetailView_DataContextChanged;
        }

        private void CustomerDetailView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.CustomerDetailViewModel oldVm)
            {
                oldVm.Contacts.CollectionChanged -= Contacts_CollectionChanged;
            }
            if (e.NewValue is ViewModels.CustomerDetailViewModel newVm)
            {
                newVm.Contacts.CollectionChanged += Contacts_CollectionChanged;
            }
        }

        private void Contacts_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
            {
                var newItem = e.NewItems[0];
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (ContactsItemsControl.ItemContainerGenerator.ContainerFromItem(newItem) is System.Windows.FrameworkElement container)
                    {
                        container.BringIntoView();
                        
                        var textBox = FindVisualChild<TextBox>(container);
                        if (textBox != null)
                        {
                            textBox.Focus();
                            // Move cursor to end if needed, though it's empty so focus is enough
                        }
                    }
                }), DispatcherPriority.ContextIdle);
            }
        }

        private static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                    return t;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
