using System.Windows.Controls;

namespace OCC.WpfClient.Features.Main.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
            DataContextChanged += MainView_DataContextChanged;
        }

        private void MainView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is System.ComponentModel.INotifyPropertyChanged oldVm)
            {
                oldVm.PropertyChanged -= Vm_PropertyChanged;
            }
            if (e.NewValue is System.ComponentModel.INotifyPropertyChanged newVm)
            {
                newVm.PropertyChanged += Vm_PropertyChanged;
            }
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsSidebarMinimized" && sender is ViewModels.MainViewModel vm)
            {
                var sb = (System.Windows.Media.Animation.Storyboard)Resources[vm.IsSidebarMinimized ? "CollapseSidebar" : "ExpandSidebar"];
                sb.Begin();
            }
        }

        private void OnSidebarClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (DataContext is ViewModels.MainViewModel vm)
                {
                    vm.ToggleSidebarCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void OnSidebarMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.IsSidebarMinimized = false;
            }
        }

        private void OnOnlineStatusClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.ToggleUserListCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnSidebarMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.IsSidebarMinimized = true;
            }
        }

        private void OnProfileCircleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.ToggleProfileMenuCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnOverlayMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.IsUserListVisible = false;
                vm.IsProfileMenuVisible = false;
            }
        }

        private void TabsListBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                var scrollViewer = GetVisualChild<ScrollViewer>(listBox);
                if (scrollViewer != null)
                {
                    if (e.Delta > 0)
                        scrollViewer.LineLeft();
                    else
                        scrollViewer.LineRight();
                    e.Handled = true;
                }
            }
        }

        private T? GetVisualChild<T>(System.Windows.Media.Visual parent) where T : System.Windows.Media.Visual
        {
            T? child = default;
            int numVisuals = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                System.Windows.Media.Visual v = (System.Windows.Media.Visual)System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }
    }
}
