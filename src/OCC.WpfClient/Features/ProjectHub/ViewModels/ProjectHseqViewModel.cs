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
        private readonly ProjectHseqDashboardViewModel _dashboardVm;
        private readonly DocumentsListViewModel _documentsVm;
        private readonly IncidentListViewModel _incidentsVm;
        private readonly AuditListViewModel _auditsVm;
        private readonly IAttendanceService _attendanceService;

        [ObservableProperty] private ViewModelBase _currentView;
        [ObservableProperty] private string _activeTab = "Dashboard";
        [ObservableProperty] private Guid _projectId;
        [ObservableProperty] private string _projectName = string.Empty;
        [ObservableProperty] private double _safeWorkingHours;

        public ProjectHseqDashboardViewModel DashboardVm => _dashboardVm;
        public DocumentsListViewModel DocumentsVm => _documentsVm;
        public IncidentListViewModel IncidentsVm => _incidentsVm;
        public AuditListViewModel AuditsVm => _auditsVm;

        public ProjectHseqViewModel(
            ProjectHseqDashboardViewModel dashboardVm,
            DocumentsListViewModel documentsVm,
            IncidentListViewModel incidentsVm,
            AuditListViewModel auditsVm,
            IAttendanceService attendanceService)
        {
            _dashboardVm = dashboardVm;
            _documentsVm = documentsVm;
            _incidentsVm = incidentsVm;
            _auditsVm = auditsVm;
            _attendanceService = attendanceService;
            _currentView = _dashboardVm;
            Title = "Project HSEQ";
        }

        /// <summary>
        /// Called by ProjectDetailViewModel when a project is loaded.
        /// Filters all HSEQ data to this project.
        /// </summary>
        public void Initialize(Guid projectId, string projectName, string? siteManagerName, bool silent = false)
        {
            ProjectId = projectId;
            ProjectName = projectName;
            _dashboardVm.Initialize(projectId, projectName);
            // Reload documents and audits filtered to this project
            _ = _documentsVm.LoadDocumentsInternal(projectId, silent);
            _auditsVm.Initialize(projectId, projectName, siteManagerName, silent);
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
        private void ShowDashboard()
        {
            ActiveTab = "Dashboard";
            CurrentView = _dashboardVm;
            _ = _dashboardVm.LoadDataAsync();
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

        [RelayCommand]
        private void ShowAudits()
        {
            ActiveTab = "Audits";
            CurrentView = _auditsVm;
        }
    }
}
