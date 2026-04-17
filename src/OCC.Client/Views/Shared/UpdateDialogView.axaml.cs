using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Views.Shared
{
    public partial class UpdateDialogView : Window
    {
        public UpdateDialogView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
