using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Linq;
using System;
using OCC.Client.Features.HseqHub.ViewModels;

namespace OCC.Client.Features.HseqHub.Views
{
    public partial class IncidentsView : UserControl
    {
        public IncidentsView()
        {
            InitializeComponent();
            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
        }

        private void DragOver(object? sender, DragEventArgs e)
        {
            #pragma warning disable CS0618 // Type or member is obsolete
            if (e.Data.Contains(DataFormats.Files))
            {
               e.DragEffects = DragDropEffects.Copy;
            }
            else
            {
               e.DragEffects = DragDropEffects.None;
            }
            #pragma warning restore CS0618 // Type or member is obsolete
        }

        private void Drop(object? sender, DragEventArgs e)
        {
            #pragma warning disable CS0618 // Type or member is obsolete
            if (DataContext is IncidentsViewModel vm && e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles()?.ToList();
                if (files != null && files.Any())
                {
                    var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                    var photos = files.Where(f => imageExtensions.Contains(System.IO.Path.GetExtension(f.Name).ToLower())).ToList();
                    var docs = files.Where(f => !imageExtensions.Contains(System.IO.Path.GetExtension(f.Name).ToLower())).ToList();

                    if (photos.Any()) vm.Editor.UploadPhotosCommand.Execute(photos);
                    if (docs.Any()) vm.Editor.UploadDocumentsCommand.Execute(docs);
                }
            }
            #pragma warning restore CS0618 // Type or member is obsolete
        }

        private async void Browse_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not IncidentsViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Photos to Upload",
                AllowMultiple = true,
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            });

            if (files != null && files.Any())
            {
                vm.Editor.UploadPhotosCommand.Execute(files);
            }
        }

        private async void BrowseDocuments_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not IncidentsViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Documents to Upload",
                AllowMultiple = true,
                FileTypeFilter = new[] 
                { 
                    FilePickerFileTypes.Pdf,
                    new FilePickerFileType("Office Documents") { Patterns = new[] { "*.doc", "*.docx", "*.xls", "*.xlsx", "*.ppt", "*.pptx" } },
                    FilePickerFileTypes.TextPlain
                }
            });

            if (files != null && files.Any())
            {
                vm.Editor.UploadDocumentsCommand.Execute(files);
            }
        }
    }
}
