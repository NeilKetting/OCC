using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    /// <summary>
    /// Lists all crew deployments for a specific project on today's date.
    /// Hosted as a sub-view inside ProjectDetailView.
    /// </summary>
    public partial class CrewDeploymentListViewModel : OverlayHostViewModel
    {
        private readonly ICrewDeploymentService _crewService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty] private ObservableCollection<SiteDeploymentDto> _deployments = new();
        [ObservableProperty] private Guid _projectId;
        [ObservableProperty] private string _projectName = string.Empty;
        [ObservableProperty] private DateTime _selectedDate = DateTime.Today;

        private readonly IPdfService _pdfService;

        public CrewDeploymentListViewModel(
            ICrewDeploymentService crewService,
            IDialogService dialogService,
            IServiceProvider serviceProvider,
            IPdfService pdfService)
        {
            _crewService = crewService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _pdfService = pdfService;
            Title = "Daily Crew";
        }

        public void Initialize(Guid projectId, string projectName)
        {
            ProjectId = projectId;
            ProjectName = projectName;
            _ = LoadDeploymentsAsync();
        }

        [RelayCommand]
        public async Task LoadDeploymentsAsync()
        {
            if (ProjectId == Guid.Empty) return;
            try
            {
                IsBusy = true;
                BusyText = "Loading crew deployments...";
                var data = await _crewService.GetDeploymentsAsync(projectId: ProjectId, date: SelectedDate);
                Deployments = new ObservableCollection<SiteDeploymentDto>(
                    data.OrderBy(d => d.Label));
            }
            catch (Exception ex)
            {
                NotifyError("Error", "Failed to load crew deployments.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void CreateCrew()
        {
            var vm = _serviceProvider.GetRequiredService<CrewBuilderViewModel>();
            vm.Initialize(ProjectId, ProjectName, SelectedDate);
            OpenOverlay(vm, OnCrewCreated);
        }

        private void OnCrewCreated(object? result)
        {
            if (result is SiteDeploymentDto dto)
            {
                Deployments.Insert(0, dto);
                NotifySuccess("Crew Sent", $"'{dto.Label}' has been sent to site ({dto.Members.Count} members).");
            }
        }

        [RelayCommand]
        private async Task CancelDeployment(SiteDeploymentDto? deployment)
        {
            if (deployment == null || deployment.Status != OCC.Shared.Models.DeploymentStatus.Pending) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Cancel Crew",
                $"Are you sure you want to cancel crew '{deployment.Label}'? The site manager will no longer see this crew.");

            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Cancelling...";
                var success = await _crewService.CancelDeploymentAsync(deployment.Id);
                if (success)
                {
                    Deployments.Remove(deployment);
                    NotifySuccess("Cancelled", $"Crew '{deployment.Label}' cancelled.");
                }
            }
            catch (Exception ex)
            {
                NotifyError("Error", "Failed to cancel deployment.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

    }
}
