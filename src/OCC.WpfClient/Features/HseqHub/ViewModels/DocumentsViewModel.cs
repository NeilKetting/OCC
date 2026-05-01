using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;
using OCC.Shared.Models;
using OCC.Shared.Enums;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class DocumentsViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;
        
        [ObservableProperty]
        private ObservableCollection<HseqDocument> _documents = new();

        [ObservableProperty]
        private bool _isUploading;

        [ObservableProperty]
        private bool _showUploadForm;

        [ObservableProperty]
        private string _newDocTitle = string.Empty;

        [ObservableProperty]
        private DocumentCategory _newDocCategory = DocumentCategory.Other;
        
        [ObservableProperty]
        private string _selectedFilePath = string.Empty;

        public DocumentCategory[] Categories { get; } = 
            (DocumentCategory[])Enum.GetValues(typeof(DocumentCategory));

        public DocumentsViewModel(IHealthSafetyService hseqService)
        {
            _hseqService = hseqService;
            Title = "Documents";
            _ = LoadDocuments();
        }

        // Design-time
        public DocumentsViewModel()
        {
             _hseqService = null!;
        }

        [RelayCommand]
        public async Task LoadDocuments()
        {
            if (_hseqService == null) return;
            IsBusy = true;
            try
            {
                var docs = await _hseqService.GetDocumentsAsync();
                if (docs != null)
                {
                    Documents = new ObservableCollection<HseqDocument>(docs.OrderByDescending(d => d.UploadDate));
                }
            }
            catch (Exception)
            {
                NotifyError("Error", "Failed to load documents.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ToggleUploadForm()
        {
            ShowUploadForm = !ShowUploadForm;
            if (ShowUploadForm)
            {
                NewDocTitle = "";
                NewDocCategory = DocumentCategory.Policy;
                SelectedFilePath = "";
            }
        }

        [RelayCommand]
        private void PickFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Document",
                Filter = "Documents|*.pdf;*.docx;*.xlsx;*.jpg;*.png"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedFilePath = dialog.FileName;
                if (string.IsNullOrEmpty(NewDocTitle))
                {
                    NewDocTitle = Path.GetFileNameWithoutExtension(dialog.FileName);
                }
            }
        }

        [RelayCommand]
        private async Task Upload()
        {
            if (string.IsNullOrWhiteSpace(NewDocTitle) || string.IsNullOrWhiteSpace(SelectedFilePath))
            {
                NotifyError("Validation", "Title and File are required.");
                return;
            }

            if (!File.Exists(SelectedFilePath))
            {
                 NotifyError("Validation", "Selected file does not exist.");
                 return;
            }

            IsUploading = true;
            try
            {
                using var stream = File.OpenRead(SelectedFilePath);
                var fileName = Path.GetFileName(SelectedFilePath);

                var metadata = new HseqDocument
                {
                    Title = NewDocTitle,
                    Category = NewDocCategory,
                    UploadedBy = "Current User",
                    UploadDate = DateTime.UtcNow,
                    Version = "1.0"
                };

                var created = await _hseqService.UploadDocumentAsync(metadata, stream, fileName);
                if (created != null)
                {
                    Documents.Insert(0, created);
                    NotifySuccess("Success", "Document uploaded.");
                    ShowUploadForm = false;
                }
                else
                {
                     NotifyError("Error", "Upload failed.");
                }
            }
            catch (Exception ex)
            {
                NotifyError("Error", $"Failed to upload: {ex.Message}");
            }
            finally
            {
                IsUploading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteDocument(HseqDocument doc)
        {
            if (doc == null) return;
            try
            {
                var success = await _hseqService.DeleteDocumentAsync(doc.Id);
                if (success)
                {
                    Documents.Remove(doc);
                    NotifySuccess("Deleted", "Document removed.");
                }
            }
            catch (Exception)
            {
                 NotifyError("Error", "Failed to delete document.");
            }
        }
        
        [RelayCommand]
        private void DownloadDocument(HseqDocument doc)
        {
            NotifySuccess("Download", $"Downloading {doc.Title}...");
        }
    }
}
