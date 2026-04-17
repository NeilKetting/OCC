using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Views
{
    public partial class TextInputDialog : Window
    {
        public static readonly StyledProperty<string> MessageProperty = 
            AvaloniaProperty.Register<TextInputDialog, string>(nameof(Message));

        public string Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public static readonly StyledProperty<string> InputValueProperty = 
            AvaloniaProperty.Register<TextInputDialog, string>(nameof(InputValue));

        public string InputValue
        {
            get => GetValue(InputValueProperty);
            set => SetValue(InputValueProperty, value);
        }

        public TextInputDialog()
        {
            InitializeComponent();
        }

        public TextInputDialog(string title, string message, string defaultValue = "") : this()
        {
            Title = title;
            Message = message;
            InputValue = defaultValue;
            DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close(InputValue);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
