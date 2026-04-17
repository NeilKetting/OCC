using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.ProjectsHub.Views
{
    public partial class ProjectFilesView : UserControl
    {
        public ProjectFilesView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
