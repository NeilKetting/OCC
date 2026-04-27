using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Mobile.Features.HSEQ
{
    public partial class HseqListView : UserControl
    {
        public HseqListView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
