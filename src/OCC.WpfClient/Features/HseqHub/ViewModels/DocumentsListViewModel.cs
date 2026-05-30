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

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class DocumentsListViewModel : OverlayHostViewModel
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IProjectService _projectService;
        private readonly IServiceProvider _serviceProvider;
        
        [ObservableProperty]
        private ObservableCollection<HseqDocument> _documents = new();

        [ObservableProperty]
        private Guid? _projectId;

        private List<Project> _allProjects = new();

        public DocumentsListViewModel(IHealthSafetyService hseqService, IProjectService projectService, IServiceProvider serviceProvider)
        {
            _hseqService = hseqService;
            _projectService = projectService;
            _serviceProvider = serviceProvider;
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
            ProjectId = projectId;
            if (_hseqService == null) return;
            IsBusy = true;
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
                NotifyError("Error", "Failed to load documents.");
            }
            finally
            {
                IsBusy = false;
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
        private void DownloadDocument(HseqDocument doc)
        {
            NotifySuccess("Download", $"Downloading {doc.Title}...");
        }
    }
}
