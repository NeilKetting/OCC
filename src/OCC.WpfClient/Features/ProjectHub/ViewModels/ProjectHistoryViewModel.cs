using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.DTOs;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectHistoryViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private Guid _projectId;

        [ObservableProperty] private ObservableCollection<PersonnelHistoryEntryDto> _entries = new();
        [ObservableProperty] private bool _isLoading;

        public ProjectHistoryViewModel(IProjectService projectService)
        {
            _projectService = projectService;
            Title = "Project History";
        }

        public async Task LoadHistoryAsync(Guid projectId)
        {
            _projectId = projectId;
            IsLoading = true;
            UpdateStatus("Loading project history...");

            try
            {
                var history = await _projectService.GetProjectHistoryAsync(projectId);
                Entries.Clear();
                if (history?.Entries != null)
                {
                    foreach (var entry in history.Entries)
                    {
                        Entries.Add(entry);
                    }
                }
                UpdateStatus("Ready");
            }
            catch (Exception)
            {
                UpdateStatus("Error loading history");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
