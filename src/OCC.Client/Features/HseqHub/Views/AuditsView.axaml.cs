using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Linq;
using System;
using OCC.Client.Features.HseqHub.ViewModels;
using OCC.Client.ModelWrappers;
using OCC.Shared.Models;

namespace OCC.Client.Features.HseqHub.Views
{
    public partial class AuditsView : UserControl
    {
        public AuditsView()
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
            if (DataContext is AuditsViewModel vm && e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                if (files != null)
                {
                    vm.Editor.UploadFilesCommand.Execute(files);
                }
            }
            #pragma warning restore CS0618 // Type or member is obsolete
        }

        private async void Browse_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not AuditsViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Images to Upload",
                AllowMultiple = true,
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            });

            if (files != null && files.Any())
            {
                vm.Editor.UploadFilesCommand.Execute(files);
            }
        }

        private async void BrowseItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is HseqAuditNonComplianceItemWrapper itemWrapper && DataContext is AuditsViewModel vm)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Image for this Finding",
                    AllowMultiple = true,
                    FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
                });

                if (files != null && files.Any())
                {
                    vm.Editor.UploadFilesCommand.Execute(new Tuple<object, HseqAuditNonComplianceItem>(files, itemWrapper.Model));
                }
            }
        }
    }
}
