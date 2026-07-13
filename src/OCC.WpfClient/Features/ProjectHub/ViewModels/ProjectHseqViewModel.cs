using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Features.HseqHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    /// <summary>
    /// Project-scoped HSEQ sub-view. Hosts Documents and Incidents filtered to the current project.
    /// Hosted inside ProjectDetailView as one of the sidebar navigation options.
    /// </summary>
    public partial class ProjectHseqViewModel : ViewModelBase
    {
        private readonly DocumentsListViewModel _documentsVm;
        private readonly IncidentListViewModel _incidentsVm;
        private readonly IAttendanceService _attendanceService;

        [ObservableProperty] private ViewModelBase _currentView;
        [ObservableProperty] private string _activeTab = "Documents";
        [ObservableProperty] private Guid _projectId;
        [ObservableProperty] private double _safeWorkingHours;

        public DocumentsListViewModel DocumentsVm => _documentsVm;
        public IncidentListViewModel IncidentsVm => _incidentsVm;

        public ProjectHseqViewModel(
            DocumentsListViewModel documentsVm,
            IncidentListViewModel incidentsVm,
            IAttendanceService attendanceService)
        {
            _documentsVm = documentsVm;
            _incidentsVm = incidentsVm;
            _attendanceService = attendanceService;
            _currentView = _documentsVm;
            Title = "Project Safety";
        }

        /// <summary>
        /// Called by ProjectDetailViewModel when a project is loaded.
        /// Filters all HSEQ data to this project.
        /// </summary>
        public void Initialize(Guid projectId, bool silent = false)
        {
            ProjectId = projectId;
            // Reload documents filtered to this project
            _ = _documentsVm.LoadDocumentsInternal(projectId, silent);
            _ = LoadSafeWorkingHoursAsync();
        }

        private async Task LoadSafeWorkingHoursAsync()
        {
            try
            {
                SafeWorkingHours = await _attendanceService.GetProjectSafeHoursAsync(ProjectId);
            }
            catch
            {
                SafeWorkingHours = 0;
            }
        }

        [RelayCommand]
        private void ShowDocuments()
        {
            ActiveTab = "Documents";
            CurrentView = _documentsVm;
        }

        [RelayCommand]
        private void ShowIncidents()
        {
            ActiveTab = "Incidents";
            CurrentView = _incidentsVm;
        }
    }
}
