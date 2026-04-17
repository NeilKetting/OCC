using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectFilesViewModel : ViewModelBase
    {
        private readonly ITaskAttachmentService _attachmentService;
        private readonly IToastService _toastService;
        private readonly IDialogService _dialogService;
        private Guid _projectId;

        [ObservableProperty]
        private ObservableCollection<TaskAttachment> _files = new();

        [ObservableProperty]
        private bool _showUploadForm;

        [ObservableProperty]
        private string _newFileTitle = string.Empty;

        [ObservableProperty]
        private string _selectedFilePath = string.Empty;

        public ProjectFilesViewModel(
            ITaskAttachmentService attachmentService,
            IToastService toastService,
            IDialogService dialogService)
        {
            _attachmentService = attachmentService;
            _toastService = toastService;
            _dialogService = dialogService;
        }

        public ProjectFilesViewModel()
        {
            _attachmentService = null!;
            _toastService = null!;
            _dialogService = null!;
        }

        public async Task LoadProjectFilesAsync(Guid projectId)
        {
            _projectId = projectId;
            IsBusy = true;
            try
            {
                // Note: Currently we only have TaskAttachment. 
                // In a full implementation, we'd have ProjectAttachment.
                // For now, we'll fetch attachments for the project if possible, 
                // or just show an empty list with mock capability.
                
                // Mocking some data if service isn't fully ready for project-level files
                // but showing the UI pattern requested.
                await Task.Delay(500); // Simulate API call
                
                Files.Clear();
                // If we had project-level files, we'd load them here.
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
                NewFileTitle = "";
                SelectedFilePath = "";
            }
        }

        [RelayCommand]
        private async Task PickFile()
        {
            var path = await _dialogService.PickFileAsync("Select File", new[] { "*.pdf", "*.docx", "*.xlsx", "*.jpg", "*.png", "*.zip" });
            if (!string.IsNullOrEmpty(path))
            {
                SelectedFilePath = path;
                if (string.IsNullOrEmpty(NewFileTitle))
                {
                    NewFileTitle = System.IO.Path.GetFileNameWithoutExtension(path);
                }
            }
        }

        [RelayCommand]
        private async Task Upload()
        {
            if (string.IsNullOrWhiteSpace(NewFileTitle) || string.IsNullOrWhiteSpace(SelectedFilePath))
            {
                _toastService.ShowError("Validation", "Title and File are required.");
                return;
            }

            IsBusy = true;
            try
            {
                // Mock upload for now
                await Task.Delay(1000);
                
                var newFile = new TaskAttachment
                {
                    Id = Guid.NewGuid(),
                    FileName = NewFileTitle,
                    UploadedBy = "Current User",
                    UploadedAt = DateTime.Now,
                    FileSize = "1.2 MB" // Mock size
                };

                Files.Insert(0, newFile);
                _toastService.ShowSuccess("Success", "File uploaded successfully.");
                ShowUploadForm = false;
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", $"Failed to upload: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteFile(TaskAttachment file)
        {
            if (file == null) return;

            var confirmed = await _dialogService.ShowConfirmationAsync("Delete File", $"Are you sure you want to delete '{file.FileName}'?");
            if (confirmed)
            {
                Files.Remove(file);
                _toastService.ShowSuccess("Deleted", "File removed.");
            }
        }

        [RelayCommand]
        private void DownloadFile(TaskAttachment file)
        {
            _toastService.ShowInfo("Download", $"Downloading {file.FileName}...");
        }
    }
}
