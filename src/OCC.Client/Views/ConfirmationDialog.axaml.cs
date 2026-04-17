using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Views
{
    public partial class ConfirmationDialog : Window
    {
        public bool Result { get; private set; }

        public ConfirmationDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public ConfirmationDialog(string title, string message) : this()
        {
            var titleBlock = this.FindControl<TextBlock>("TitleText");
            var messageBlock = this.FindControl<TextBlock>("MessageText");

            if (titleBlock != null) titleBlock.Text = title;
            if (messageBlock != null) messageBlock.Text = message;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void Ok_Click(object? sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }
    }
}
