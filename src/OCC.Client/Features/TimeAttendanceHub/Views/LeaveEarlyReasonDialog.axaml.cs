using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System.Linq;

namespace OCC.Client.Features.TimeAttendanceHub.Views
{
    public partial class LeaveEarlyReasonDialog : Window
    {
        public string? Reason => (this.FindControl<ComboBox>("ReasonComboBox")?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        public string? Note => this.FindControl<TextBox>("NoteTextBox")?.Text;
        public string? SelectedFilePath { get; private set; }

        public LeaveEarlyReasonDialog()
        {
            InitializeComponent();
            
            var reasonComboBox = this.FindControl<ComboBox>("ReasonComboBox");
            if (reasonComboBox != null)
            {
                reasonComboBox.SelectionChanged += ReasonComboBox_SelectionChanged;
            }
        }

        private void ReasonComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var attachmentPanel = this.FindControl<StackPanel>("AttachmentPanel");
            if (attachmentPanel != null)
            {
                attachmentPanel.IsVisible = Reason == "Sick";
            }
        }

        private async void OnBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Medical Certificate",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images/PDF") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.pdf" } },
                    FilePickerFileTypes.All
                }
            });

            if (files.Count > 0)
            {
                SelectedFilePath = files[0].Path.LocalPath;
                var textBlock = this.FindControl<TextBlock>("FilePathText");
                if (textBlock != null)
                {
                    textBlock.Text = System.IO.Path.GetFileName(SelectedFilePath);
                    textBlock.Foreground = Avalonia.Media.Brushes.Black;
                }
            }
        }

        private void OnConfirmClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(true);
        }

        private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
