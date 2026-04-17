using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.DTOs;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectReportViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<ProjectReportViewModel> _logger;

        [ObservableProperty]
        private ProjectReportDto? _report;

        [ObservableProperty]
        private Guid _projectId;

        public ProjectReportViewModel(IProjectService projectService, IDialogService dialogService, ILogger<ProjectReportViewModel> logger)
        {
            _projectService = projectService;
            _dialogService = dialogService;
            _logger = logger;
            
            Title = "Project Cost Report";
        }

        public async Task LoadReportAsync(Guid projectId)
        {
            ProjectId = projectId;
            await RefreshCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        public async Task Refresh()
        {
            if (ProjectId == Guid.Empty) return;

            try
            {
                IsBusy = true;
                BusyText = "Generating project report...";
                
                var data = await _projectService.GetProjectReportAsync(ProjectId);
                if (data == null)
                {
                    await _dialogService.ShowAlertAsync("Error", "Failed to load project report.");
                    return;
                }

                Report = data;
                Title = $"Report: {Report.ProjectName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load report for project {ProjectId}", ProjectId);
                await _dialogService.ShowAlertAsync("Error", "An unexpected error occurred while loading the report.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task Print()
        {
            // Future requirement: PDF generation
            await _dialogService.ShowAlertAsync("Print", "Printing report functionality is under development.");
        }

        [RelayCommand]
        public void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? CloseRequested;
    }
}
