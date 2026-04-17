using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Features.AuthHub.ViewModels;

namespace OCC.WpfClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Features.Shell.ViewModels.ShellViewModel _viewModel;

        public MainWindow(Features.Shell.ViewModels.ShellViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            DataContext = _viewModel;

            StateChanged += MainWindow_StateChanged;

            // Register for resize messages
            WeakReferenceMessenger.Default.Register<ResizeWindowMessage>(this, (r, m) => ((MainWindow)r).Receive(m));

            // Set initial view
            _viewModel.Navigation.NavigateTo<AuthViewModel>();
        }

        private void MainWindow_StateChanged(object? sender, System.EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                BtnMaximize.Content = ""; // Restore icon
            }
            else
            {
                BtnMaximize.Content = ""; // Maximize icon
            }
        }

        private void OnMinimizeClick(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        public void Receive(ResizeWindowMessage message)
        {
            var info = message.Value;
            if (info.Width > 0) Width = info.Width;
            if (info.Height > 0) Height = info.Height;
            WindowState = info.State;
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}