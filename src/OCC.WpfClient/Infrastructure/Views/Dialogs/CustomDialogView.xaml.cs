using System.Windows;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Infrastructure.Views.Dialogs
{
    public partial class CustomDialogView : Window
    {
        public CustomDialogResult Result { get; private set; } = CustomDialogResult.Cancel;

        public CustomDialogView(string title, string message, string primaryText, string secondaryText, string cancelText)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            
            BtnPrimary.Content = primaryText;
            BtnSecondary.Content = secondaryText;
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
