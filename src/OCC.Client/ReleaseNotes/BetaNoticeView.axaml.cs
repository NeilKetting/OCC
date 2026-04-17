using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.ReleaseNotes
{
    public partial class BetaNoticeView : UserControl
    {
        public BetaNoticeView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
