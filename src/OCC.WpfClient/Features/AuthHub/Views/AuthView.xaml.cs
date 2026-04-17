using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using OCC.WpfClient.Features.AuthHub.ViewModels;

namespace OCC.WpfClient.Features.AuthHub.Views
{
    public partial class AuthView : UserControl
    {
        public AuthView()
        {
            InitializeComponent();
        }

        private void OnAuthViewLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AuthViewModel vm)
            {
                if (string.IsNullOrEmpty(vm.LoginModel.Email))
                {
                    EmailTextBox.Focus();
                }
                else
                {
                    // Small delay to ensure the control is ready for focus
                    Dispatcher.BeginInvoke(new System.Action(() => LoginPasswordBox.Focus()), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }

        private void OnLoginFieldKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (DataContext is AuthViewModel vm)
                {
                    if (vm.LoginCommand.CanExecute(LoginPasswordBox))
                    {
                        vm.LoginCommand.Execute(LoginPasswordBox);
                    }
                }
            }
        }

        private void OnRegisterClick(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)this.Resources["FlipToRegister"];
            if (sb != null) sb.Begin();
        }

        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)this.Resources["FlipToLogin"];
            if (sb != null) sb.Begin();
        }

        private void OnForgotPasswordClick(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)this.Resources["FlipToForgotPassword"];
            if (sb != null) sb.Begin();
        }
    }
}
