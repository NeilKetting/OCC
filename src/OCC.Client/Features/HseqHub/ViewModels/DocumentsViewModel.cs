using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.HseqHub.ViewModels
{
    public partial class DocumentsViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IToastService _toastService;
        private readonly IDialogService _dialogService;
        
        [ObservableProperty]
        private ObservableCollection<HseqDocument> _documents = new();

        [ObservableProperty]
        private bool _isUploading;

        // Upload Form Properties
        [ObservableProperty]
        private bool _showUploadForm;

        [ObservableProperty]
        private string _newDocTitle = string.Empty;

        [ObservableProperty]
        private OCC.Shared.Enums.DocumentCategory _newDocCategory = OCC.Shared.Enums.DocumentCategory.Other;
        
        [ObservableProperty]
        private string _selectedFilePath = string.Empty;

        public OCC.Shared.Enums.DocumentCategory[] Categories { get; } = 
            (OCC.Shared.Enums.DocumentCategory[])Enum.GetValues(typeof(OCC.Shared.Enums.DocumentCategory));

        public DocumentsViewModel(IHealthSafetyService hseqService, IToastService toastService, IDialogService dialogService)
        {
            _hseqService = hseqService;
            _toastService = toastService;
            _dialogService = dialogService;
        }

        public DocumentsViewModel()
        {
             _hseqService = null!;
             _toastService = null!;
             _dialogService = null!;
        }

        [RelayCommand]
        public async Task LoadDocuments()
        {
            if (_hseqService == null) return;
            IsBusy = true;
            try
            {
                var docs = await _hseqService.GetDocumentsAsync();
                Documents = new ObservableCollection<HseqDocument>(docs.OrderByDescending(d => d.UploadDate));
            }
            catch (Exception)
            {
                _toastService.ShowError("Error", "Failed to load documents.");
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
                NewDocCategory = OCC.Shared.Enums.DocumentCategory.Policy;
                SelectedFilePath = "";
            }
        }

        [RelayCommand]
        private async Task PickFile()
        {
            var path = await _dialogService.PickFileAsync("Select Document", new[] { "*.pdf", "*.docx", "*.xlsx", "*.jpg", "*.png" });
            if (!string.IsNullOrEmpty(path))
            {
                SelectedFilePath = path;
                if (string.IsNullOrEmpty(NewDocTitle))
                {
                    NewDocTitle = System.IO.Path.GetFileNameWithoutExtension(path);
                }
            }
        }

        [RelayCommand]
        private async Task Upload()
        {
            if (string.IsNullOrWhiteSpace(NewDocTitle) || string.IsNullOrWhiteSpace(SelectedFilePath))
            {
                _toastService.ShowError("Validation", "Title and File are required.");
                return;
            }

            if (!System.IO.File.Exists(SelectedFilePath))
            {
                 _toastService.ShowError("Validation", "Selected file does not exist.");
                 return;
            }

            IsUploading = true;
            try
            {
                using var stream = System.IO.File.OpenRead(SelectedFilePath);
                var fileName = System.IO.Path.GetFileName(SelectedFilePath);

                var metadata = new HseqDocument
                {
                    Title = NewDocTitle,
                    Category = NewDocCategory,
                    UploadedBy = "Current User", // Should be replaced by Auth Service user
                    UploadDate = DateTime.UtcNow,
                    Version = "1.0"
                };

                var created = await _hseqService.UploadDocumentAsync(metadata, stream, fileName);
                if (created != null)
                {
                    Documents.Insert(0, created);
                    _toastService.ShowSuccess("Success", "Document uploaded.");
                    ShowUploadForm = false;
                }
                else
                {
                     _toastService.ShowError("Error", "Upload failed.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", $"Failed to upload: {ex.Message}");
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
                    _toastService.ShowSuccess("Deleted", "Document removed.");
                }
            }
            catch (Exception)
            {
                 _toastService.ShowError("Error", "Failed to delete document.");
            }
        }
        
        [RelayCommand]
        private void DownloadDocument(HseqDocument doc)
        {
            _toastService.ShowInfo("Download", $"Downloading {doc.Title}...");
        }
    }
}
