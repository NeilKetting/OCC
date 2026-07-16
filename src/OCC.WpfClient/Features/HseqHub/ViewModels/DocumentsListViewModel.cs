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
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Net.Http;
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class DocumentsListViewModel : OverlayHostViewModel
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IProjectService _projectService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ConnectionSettings _connectionSettings;
        
        [ObservableProperty]
        private ObservableCollection<HseqDocument> _documents = new();

        [ObservableProperty]
        private Guid? _projectId;

        private List<Project> _allProjects = new();

        public DocumentsListViewModel(
            IHealthSafetyService hseqService, 
            IProjectService projectService, 
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            ConnectionSettings connectionSettings)
        {
            _hseqService = hseqService;
            _projectService = projectService;
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _connectionSettings = connectionSettings;
            Title = "Documents";
            _ = LoadDocuments();
            _ = LoadProjects();
        }

        private async Task LoadProjects()
        {
            try
            {
                var projects = await _projectService.GetProjectsAsync();
                if (projects != null)
                {
                    _allProjects = projects.OrderBy(x => x.Name).ToList();
                }
            }
            catch { /* Silent fail for projects */ }
        }

        [RelayCommand]
        public async Task LoadDocuments(Guid? projectId = null)
        {
            await LoadDocumentsInternal(projectId, false);
        }

        public async Task LoadDocumentsInternal(Guid? projectId = null, bool silent = false)
        {
            ProjectId = projectId;
            if (_hseqService == null) return;
            if (!silent)
            {
                IsBusy = true;
            }
            try
            {
                var docs = await _hseqService.GetDocumentsAsync(projectId);
                if (docs != null)
                {
                    Documents = new ObservableCollection<HseqDocument>(docs.OrderByDescending(d => d.UploadDate));
                }
            }
            catch (Exception)
            {
                if (!silent)
                {
                    NotifyError("Error", "Failed to load documents.");
                }
            }
            finally
            {
                if (!silent)
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private void OpenUpload()
        {
            var vm = _serviceProvider.GetRequiredService<DocumentDetailViewModel>();
            vm.Initialize(_allProjects, ProjectId);
            OpenOverlay(vm, OnDocumentUploaded);
        }

        private void OnDocumentUploaded(object? result)
        {
            if (result is HseqDocument doc)
            {
                Documents.Insert(0, doc);
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
        private async Task DownloadDocument(HseqDocument doc)
        {
            if (doc == null || string.IsNullOrEmpty(doc.FilePath)) return;

            try
            {
                var fileName = Path.GetFileName(doc.FilePath);
                var ext = Path.GetExtension(doc.FilePath);
                if (string.IsNullOrEmpty(ext)) ext = ".pdf";

                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = fileName,
                    DefaultExt = ext,
                    Filter = $"Files (*{ext})|*{ext}|All Files (*.*)|*.*",
                    Title = "Save Document"
                };

                if (sfd.ShowDialog() == true)
                {
                    IsBusy = true;
                    NotifySuccess("Download", $"Downloading {doc.Title}...");

                    using var client = _httpClientFactory.CreateClient();
                    var token = _authService.CurrentToken;
                    if (!string.IsNullOrEmpty(token))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }

                    var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                    var fullUrl = doc.FilePath.StartsWith("http") ? doc.FilePath : $"{baseUrl}/{doc.FilePath.TrimStart('/')}";
                    
                    var bytes = await client.GetByteArrayAsync(fullUrl);
                    await File.WriteAllBytesAsync(sfd.FileName, bytes);

                    NotifySuccess("Success", $"Document saved to {Path.GetFileName(sfd.FileName)}");
                    
                    var saveDir = Path.GetDirectoryName(sfd.FileName);
                    if (!string.IsNullOrEmpty(saveDir))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = saveDir, UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                NotifyError("Error", $"Failed to download document: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
