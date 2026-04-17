using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Views.Shared
{
    public partial class ProfileView : UserControl
    {
        public ProfileView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void OnUploadPhotoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Profile Picture",
                AllowMultiple = false,
                FileTypeFilter = new[] { Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll }
            });

            if (files.Count >= 1)
            {
                var file = files[0];
                if (DataContext is OCC.Client.ViewModels.Shared.ProfileViewModel vm)
                {
                     using var stream = await file.OpenReadAsync();
                     await vm.SetProfilePictureAsync(stream);
                }
            }
        }
    }
}
