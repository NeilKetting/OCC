using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Enums;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.HseqHub.ViewModels
{
    public partial class IncidentEditorViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private Incident _incident = new() { Date = DateTime.Now };

        [ObservableProperty]
        private ObservableCollection<IncidentPhotoDto> _photos = new();

        [ObservableProperty]
        private ObservableCollection<IncidentDocumentDto> _documents = new();

        [ObservableProperty]
        private bool _isOpen;

        public IncidentType[] IncidentTypes => Enum.GetValues<IncidentType>();
        public IncidentSeverity[] IncidentSeverities => Enum.GetValues<IncidentSeverity>();
        public IncidentStatus[] IncidentStatuses => Enum.GetValues<IncidentStatus>();

        // Callback to notify parent when an incident is created/updated
        public Func<IncidentSummaryDto, Task>? OnSaved { get; set; }

        public IncidentEditorViewModel(IHealthSafetyService hseqService, IToastService toastService, IAuthService authService)
        {
            _hseqService = hseqService;
            _toastService = toastService;
            _authService = authService;
        }

        public void Initialize(Incident incident, IEnumerable<IncidentPhotoDto>? photos = null, IEnumerable<IncidentDocumentDto>? docs = null)
        {
            Incident = incident;
            Photos = new ObservableCollection<IncidentPhotoDto>(photos ?? Enumerable.Empty<IncidentPhotoDto>());
            Documents = new ObservableCollection<IncidentDocumentDto>(docs ?? Enumerable.Empty<IncidentDocumentDto>());
            IsOpen = true;
        }

        public void Clear()
        {
            Incident = new Incident { Date = DateTime.Now };
            Photos.Clear();
            Documents.Clear();
            IsOpen = false;
        }

        [RelayCommand]
        public async Task SaveIncident()
        {
            if (string.IsNullOrWhiteSpace(Incident.Description) || string.IsNullOrWhiteSpace(Incident.Location))
            {
                _toastService.ShowWarning("Validation", "Description and Location are required.");
                return;
            }

            try
            {
                IsBusy = true;
                BusyText = "Saving incident...";
                
                if (_authService.CurrentUser != null)
                {
                    Incident.ReportedByUserId = _authService.CurrentUser.Id.ToString();
                }

                var result = await _hseqService.CreateIncidentAsync(Incident);
                if (result != null)
                {
                    _toastService.ShowSuccess("Success", "Incident saved successfully.");
                    
                    var summary = new IncidentSummaryDto 
                    { 
                        Id = result.Id, 
                        Date = result.Date,
                        Location = result.Location,
                        Status = result.Status,
                        Severity = result.Severity,
                        Type = result.Type
                    };

                    if (OnSaved != null) await OnSaved(summary);
                    IsOpen = false;
                }
                else
                {
                    _toastService.ShowError("Error", "Failed to save incident.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "An error occurred while saving.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task UploadPhotos(object param)
        {
            if (param == null) return;
            
            IEnumerable<IStorageFile>? storageFiles = null;
            if (param is IEnumerable<IStorageFile> sFiles) storageFiles = sFiles;
            else if (param is IEnumerable<IStorageItem> sItems) storageFiles = sItems.OfType<IStorageFile>();
            else if (param is IStorageFile sFile) storageFiles = new[] { sFile };

            if (storageFiles == null || !storageFiles.Any()) return;

            IsBusy = true;
            try
            {
                // If it's a NEW incident, save it first to get an ID for photos
                if (Incident.Id == Guid.Empty)
                {
                    BusyText = "Saving report first...";
                    var created = await _hseqService.CreateIncidentAsync(Incident);
                    if (created == null)
                    {
                        _toastService.ShowError("Error", "Could not save incident to allow photo uploads.");
                        return;
                    }
                    Incident.Id = created.Id;
                    
                    var summary = new IncidentSummaryDto 
                    { 
                        Id = created.Id, 
                        Date = created.Date,
                        Location = created.Location,
                        Status = created.Status,
                        Severity = created.Severity,
                        Type = created.Type
                    };
                    if (OnSaved != null) await OnSaved(summary);
                }

                int count = 0;
                foreach (var file in storageFiles)
                {
                    BusyText = $"Uploading {file.Name}...";
                    using var stream = await file.OpenReadAsync();
                    var metadata = new IncidentPhoto
                    {
                        IncidentId = Incident.Id,
                        FileName = file.Name
                    };

                    var result = await _hseqService.UploadIncidentPhotoAsync(metadata, stream, file.Name);
                    if (result != null)
                    {
                        count++;
                        Photos.Add(result);
                    }
                }

                if (count > 0) _toastService.ShowSuccess("Success", $"Uploaded {count} photo(s).");
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Failed to upload photo(s).");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task DeletePhoto(IncidentPhotoDto photo)
        {
            if (photo == null) return;

            try
            {
                var success = await _hseqService.DeleteIncidentPhotoAsync(photo.Id);
                if (success)
                {
                    Photos.Remove(photo);
                    _toastService.ShowSuccess("Deleted", "Photo removed.");
                }
                else
                {
                    _toastService.ShowError("Error", "Failed to delete photo.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Exception deleting photo.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        public void ViewAttachment(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                var baseUrl = OCC.Client.Services.Infrastructure.ConnectionSettings.Instance.ApiBaseUrl.TrimEnd('/');
                var fullUrl = filePath.StartsWith("http") ? filePath : $"{baseUrl}/{filePath.TrimStart('/')}";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fullUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Could not open attachment.");
                System.Diagnostics.Debug.WriteLine($"ViewAttachment Error: {ex}");
            }
        }

        [RelayCommand]
        public async Task UploadDocuments(object param)
        {
            if (param == null) return;

            IEnumerable<IStorageFile>? storageFiles = null;
            if (param is IEnumerable<IStorageFile> sFiles) storageFiles = sFiles;
            else if (param is IEnumerable<IStorageItem> sItems) storageFiles = sItems.OfType<IStorageFile>();
            else if (param is IStorageFile sFile) storageFiles = new[] { sFile };

            if (storageFiles == null || !storageFiles.Any()) return;

            IsBusy = true;
            try
            {
                // If it's a NEW incident, save it first to get an ID for documents
                if (Incident.Id == Guid.Empty)
                {
                    BusyText = "Saving report first...";
                    var created = await _hseqService.CreateIncidentAsync(Incident);
                    if (created == null)
                    {
                        _toastService.ShowError("Error", "Could not save incident to allow document uploads.");
                        return;
                    }
                    Incident.Id = created.Id;

                    var summary = new IncidentSummaryDto
                    {
                        Id = created.Id,
                        Date = created.Date,
                        Location = created.Location,
                        Status = created.Status,
                        Severity = created.Severity,
                        Type = created.Type
                    };
                    if (OnSaved != null) await OnSaved(summary);
                }

                int count = 0;
                foreach (var file in storageFiles)
                {
                    BusyText = $"Uploading {file.Name}...";
                    using var stream = await file.OpenReadAsync();
                    var result = await _hseqService.UploadIncidentDocumentAsync(Incident.Id, stream, file.Name);
                    if (result != null)
                    {
                        count++;
                        Documents.Add(result);
                    }
                }

                if (count > 0) _toastService.ShowSuccess("Success", $"Uploaded {count} document(s).");
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Failed to upload document(s).");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task DeleteDocument(IncidentDocumentDto doc)
        {
            if (doc == null) return;

            try
            {
                var success = await _hseqService.DeleteIncidentDocumentAsync(doc.Id);
                if (success)
                {
                    Documents.Remove(doc);
                    _toastService.ShowSuccess("Deleted", "Document removed.");
                }
                else
                {
                    _toastService.ShowError("Error", "Failed to delete document.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Exception deleting document.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }
}
