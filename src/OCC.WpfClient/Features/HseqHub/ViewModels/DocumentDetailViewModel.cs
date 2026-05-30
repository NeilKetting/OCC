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
using System.Collections.Generic;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class DocumentDetailViewModel : OverlayViewModel
    {
        private readonly IHealthSafetyService _hseqService;
        
        [ObservableProperty]
        private string _newDocTitle = string.Empty;

        [ObservableProperty]
        private DocumentCategory _newDocCategory = DocumentCategory.Other;
        
        [ObservableProperty]
        private string _selectedFilePath = string.Empty;

        [ObservableProperty]
        private Project? _selectedProject;

        [ObservableProperty]
        private bool _isProjectSelectionEnabled = true;

        public ObservableCollection<Project> AvailableProjects { get; } = new();

        public DocumentCategory[] Categories { get; } = 
            (DocumentCategory[])Enum.GetValues(typeof(DocumentCategory));

        public DocumentDetailViewModel(IHealthSafetyService hseqService)
        {
            _hseqService = hseqService;
            Title = "Upload Document";
        }

        public void Initialize(IEnumerable<Project> projects, Guid? preSelectedProjectId = null)
        {
            AvailableProjects.Clear();
            foreach (var p in projects.OrderBy(x => x.Name)) AvailableProjects.Add(p);
            
            NewDocTitle = "";
            NewDocCategory = DocumentCategory.Policy;
            SelectedFilePath = "";
            
            if (preSelectedProjectId.HasValue)
            {
                SelectedProject = AvailableProjects.FirstOrDefault(p => p.Id == preSelectedProjectId.Value);
                IsProjectSelectionEnabled = false;
            }
            else
            {
                SelectedProject = null;
                IsProjectSelectionEnabled = true;
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

            IsBusy = true;
            BusyText = "Uploading document...";
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
                    Version = "1.0",
                    ProjectId = SelectedProject?.Id
                };

                var created = await _hseqService.UploadDocumentAsync(metadata, stream, fileName);
                if (created != null)
                {
                    NotifySuccess("Success", "Document uploaded.");
                    Close(created);
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
                IsBusy = false;
            }
        }
    }
}
