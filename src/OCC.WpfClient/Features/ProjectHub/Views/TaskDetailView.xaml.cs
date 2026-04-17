using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using OCC.WpfClient.Features.ProjectHub.ViewModels;

namespace OCC.WpfClient.Features.ProjectHub.Views
{
    public partial class TaskDetailView : UserControl
    {
        public TaskDetailView()
        {
            InitializeComponent();
            this.Loaded += TaskDetailView_Loaded;
        }

        private void TaskDetailView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is TaskDetailViewModel viewModel)
            {
                viewModel.CloseInitiated += (s, args) => CloseDrawer();
                
                // Hook into Escape key
                Window window = Window.GetWindow(this);
                if (window != null)
                {
                    window.PreviewKeyDown += Window_PreviewKeyDown;
                }
            }
            OpenDrawer();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && this.Visibility == Visibility.Visible)
            {
                if (DataContext is TaskDetailViewModel vm)
                {
                    vm.RequestCloseCommand.Execute(null);
                }
                e.Handled = true;
            }
        }

        private void OpenDrawer()
        {
            Storyboard? sb = this.Resources["OpenDrawer"] as Storyboard;
            sb?.Begin();
        }

        private void CloseDrawer()
        {
            Storyboard? sb = this.Resources["CloseDrawer"] as Storyboard;
            if (sb != null)
            {
                sb.Completed += (s, e) =>
                {
                    if (DataContext is TaskDetailViewModel vm)
                    {
                        vm.ConfirmClose();
                    }
                };
                sb.Begin();
            }
        }

        private void Dimmer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is TaskDetailViewModel vm)
            {
                vm.RequestCloseCommand.Execute(null);
            }
        }
    }
}
