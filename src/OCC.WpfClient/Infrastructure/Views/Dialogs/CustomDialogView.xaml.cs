using System.Windows;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Infrastructure.Views.Dialogs
{
    public partial class CustomDialogView : Window
    {
        public CustomDialogResult Result { get; private set; } = CustomDialogResult.Cancel;

        public CustomDialogView(string title, string message, string primaryText, string? secondaryText = null, string cancelText = "Cancel")
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            
            BtnPrimary.Content = primaryText;
            
            if (string.IsNullOrEmpty(secondaryText))
            {
                BtnSecondary.Visibility = Visibility.Collapsed;
            }
            else
            {
                BtnSecondary.Content = secondaryText;
                BtnSecondary.Visibility = Visibility.Visible;
            }

            BtnCancel.Content = cancelText;

            // Make it center relative to main window
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                this.Owner = Application.Current.MainWindow;
            }
        }

        private void BtnPrimary_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomDialogResult.Primary;
            DialogResult = true;
            Close();
        }

        private void BtnSecondary_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomDialogResult.Secondary;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomDialogResult.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
