using System.Windows;

namespace OCC.WpfClient.Infrastructure.Views.Dialogs
{
    public partial class InputDialogView : Window
    {
        public string InputValue { get; private set; } = string.Empty;

        public InputDialogView(string title, string message, string defaultValue = "")
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            InputTxt.Text = defaultValue;
            InputTxt.Focus();
            if (InputTxt.Text.Length > 0)
            {
                InputTxt.SelectAll();
            }

            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                this.Owner = Application.Current.MainWindow;
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            InputValue = InputTxt.Text;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
