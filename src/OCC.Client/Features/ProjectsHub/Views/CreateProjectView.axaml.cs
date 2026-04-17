using Avalonia.Controls;
using OCC.Client.Features.ProjectsHub.ViewModels;

namespace OCC.Client.Features.ProjectsHub.Views
{
    public partial class CreateProjectView : UserControl
    {
        public CreateProjectView()
        {
            InitializeComponent();
        }

        private async void ImportFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.CreateProjectViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Import MS Project XML",
                AllowMultiple = false,
                FileTypeFilter = new[] 
                { 
                     new Avalonia.Platform.Storage.FilePickerFileType("MS Project XML") { Patterns = new[] { "*.xml" } } 
                }
            });

            if (files.Count >= 1)
            {
                using var stream = await files[0].OpenReadAsync();
                await vm.ImportProjectAsync(stream);
            }
        }
    }
}




