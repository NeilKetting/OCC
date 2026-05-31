using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class DailyCrewBuilderViewModel : ViewModelBase
    {
        private readonly ICrewDeploymentService _crewService;
        private readonly IProjectService _projectService;
        private readonly IDialogService _dialogService;
        private readonly IEmployeeService _employeeService;

        [ObservableProperty] private ObservableCollection<ProjectSummaryDto> _projects = new();
        [ObservableProperty] private ProjectSummaryDto? _selectedProject;
        [ObservableProperty] private DateTime _deploymentDate = DateTime.Today;
        [ObservableProperty] private DateTime _assignmentEndDate = DateTime.Today;
        [ObservableProperty] private bool _excludeWeekends = true;
        [ObservableProperty] private string _crewLabel = string.Empty;
        [ObservableProperty] private ObservableCollection<SelectableDailyEmployee> _availableEmployees = new();
        [ObservableProperty] private ObservableCollection<SiteDeploymentDto> _activeDeployments = new();
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private string _validationMessage = string.Empty;

        private List<SelectableDailyEmployee> _allEmployees = new();

        public int SelectedCount => AvailableEmployees.Count(e => e.IsSelected);

        public DailyCrewBuilderViewModel(
            ICrewDeploymentService crewService,
            IProjectService projectService,
            IDialogService dialogService,
            IEmployeeService employeeService)
        {
            _crewService = crewService;
            _projectService = projectService;
            _dialogService = dialogService;
            _employeeService = employeeService;
            
            Title = "Daily Crew Builder";
            _crewLabel = $"Crew — {DeploymentDate:dd MMM yyyy}";
            _assignmentEndDate = DeploymentDate;

            _ = LoadDataAsync();
        }

        partial void OnDeploymentDateChanged(DateTime value)
        {
            CrewLabel = $"Crew — {value:dd MMM yyyy}";
            if (AssignmentEndDate < value)
            {
                AssignmentEndDate = value;
            }
            _ = LoadDataAsync();
        }

        partial void OnSelectedProjectChanged(ProjectSummaryDto? value)
        {
            if (value != null)
            {
                CrewLabel = $"{value.Name} — {DeploymentDate:dd MMM yyyy}";
            }
        }

        partial void OnSearchQueryChanged(string value) => ApplyFilter();

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                ValidationMessage = string.Empty;

                // Load active projects, all active employees, and today's deployments in parallel
                var projectsTask = _projectService.GetProjectSummariesAsync(false);
                var employeesTask = _employeeService.GetEmployeesAsync();
                var deploymentsTask = _crewService.GetDeploymentsAsync(null, DeploymentDate);

                await Task.WhenAll(projectsTask, employeesTask, deploymentsTask);

                // 1. Projects combobox source
                Projects = new ObservableCollection<ProjectSummaryDto>(
                    projectsTask.Result.Where(p => p.IsActive).OrderBy(p => p.Name));

                if (SelectedProject == null && Projects.Any())
                {
                    SelectedProject = Projects.First();
                }

                // 2. Active deployments list
                var activeDeps = deploymentsTask.Result
                    .Where(d => d.Status != DeploymentStatus.Cancelled)
                    .OrderBy(d => d.ProjectName)
                    .ThenBy(d => d.Label)
                    .ToList();
                ActiveDeployments = new ObservableCollection<SiteDeploymentDto>(activeDeps);

                // 3. Available employees list with assignment status mapping
                var employees = employeesTask.Result;
                _allEmployees = employees
                    .Where(e => e.Status == EmployeeStatus.Active)
                    .Select(emp =>
                    {
                        var selectable = new SelectableDailyEmployee(emp);
                        
                        // Check if employee is in any active deployment today
                        var matchingDeployment = activeDeps.FirstOrDefault(d => 
                            d.Members.Any(m => m.EmployeeId == emp.Id));

                        if (matchingDeployment != null)
                        {
                            selectable.IsDeployed = true;
                            selectable.DeployedProjectName = matchingDeployment.ProjectName;
                            selectable.DeployedCrewLabel = matchingDeployment.Label;
                        }
                        else
                        {
                            selectable.IsDeployed = false;
                            selectable.DeployedProjectName = string.Empty;
                            selectable.DeployedCrewLabel = string.Empty;
                        }

                        selectable.PropertyChanged += (_, e) =>
                        {
                            if (e.PropertyName == nameof(SelectableDailyEmployee.IsSelected))
                            {
                                OnPropertyChanged(nameof(SelectedCount));
                            }
                        };

                        return selectable;
                    }).ToList();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                NotifyError("Error Loading Data", "An error occurred while loading crew data.");
                System.Diagnostics.Debug.WriteLine($"DailyCrewBuilderViewModel.LoadDataAsync: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            IEnumerable<SelectableDailyEmployee> filtered = _allEmployees;
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLower();
                filtered = filtered.Where(e =>
                    (e.FullName?.ToLower().Contains(q) ?? false) ||
                    (e.Role?.ToLower().Contains(q) ?? false) ||
                    (e.AssignmentStatusText?.ToLower().Contains(q) ?? false));
            }
            AvailableEmployees = new ObservableCollection<SelectableDailyEmployee>(filtered);
            OnPropertyChanged(nameof(SelectedCount));
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var e in AvailableEmployees)
            {
                e.IsSelected = true;
            }
            OnPropertyChanged(nameof(SelectedCount));
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var e in _allEmployees)
            {
                e.IsSelected = false;
            }
            OnPropertyChanged(nameof(SelectedCount));
        }

        [RelayCommand]
        private async Task DeployCrew()
        {
            ValidationMessage = string.Empty;

            if (SelectedProject == null)
            {
                ValidationMessage = "Please select a project to deploy the crew to.";
                return;
            }

            if (string.IsNullOrWhiteSpace(CrewLabel))
            {
                ValidationMessage = "Please enter a crew label.";
                return;
            }

            var selected = _allEmployees.Where(e => e.IsSelected).ToList();
            if (!selected.Any())
            {
                ValidationMessage = "Please select at least one employee.";
                return;
            }

            if (AssignmentEndDate < DeploymentDate)
            {
                ValidationMessage = "End date cannot be before start date.";
                return;
            }

            try
            {
                IsBusy = true;
                BusyText = "Deploying crew...";

                var datesToDeploy = new List<DateTime>();
                for (var date = DeploymentDate.Date; date <= AssignmentEndDate.Date; date = date.AddDays(1))
                {
                    if (ExcludeWeekends && (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday))
                    {
                        continue;
                    }
                    datesToDeploy.Add(date);
                }

                if (!datesToDeploy.Any())
                {
                    ValidationMessage = "No valid dates selected to deploy (check weekend exclusions).";
                    return;
                }

                int successCount = 0;

                foreach (var date in datesToDeploy)
                {
                    var label = datesToDeploy.Count > 1 
                        ? $"{CrewLabel} ({date:dd MMM})"
                        : CrewLabel;

                    var request = new CreateSiteDeploymentRequest
                    {
                        ProjectId = SelectedProject.Id,
                        DeploymentDate = date,
                        Label = label,
                        MemberEmployeeIds = selected.Select(e => e.Id).ToList()
                    };

                    var created = await _crewService.CreateDeploymentAsync(request);
                    if (created != null)
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    NotifySuccess("Crew Deployed", $"Deployed crew to {SelectedProject.Name} for {successCount} day(s) ({selected.Count} members).");
                    ClearSelection();
                    await LoadDataAsync();
                }
                else
                {
                    ValidationMessage = "Failed to deploy crew. Please try again.";
                }
            }
            catch (Exception ex)
            {
                ValidationMessage = "An error occurred during crew deployment.";
                System.Diagnostics.Debug.WriteLine($"DailyCrewBuilderViewModel.DeployCrew: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CancelDeployment(SiteDeploymentDto? deployment)
        {
            if (deployment == null) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Cancel Deployment",
                $"Are you sure you want to cancel/recall crew '{deployment.Label}' for project '{deployment.ProjectName}'?");

            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Cancelling deployment...";

                var success = await _crewService.CancelDeploymentAsync(deployment.Id);
                if (success)
                {
                    NotifySuccess("Deployment Cancelled", $"Crew '{deployment.Label}' has been recalled.");
                    await LoadDataAsync();
                }
                else
                {
                    NotifyError("Cancel Failed", "Could not cancel the deployment.");
                }
            }
            catch (Exception ex)
            {
                NotifyError("Error Cancelling", "An error occurred while cancelling the deployment.");
                System.Diagnostics.Debug.WriteLine($"DailyCrewBuilderViewModel.CancelDeployment: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public partial class SelectableDailyEmployee : ObservableObject
    {
        public Guid Id { get; }
        public string FullName { get; }
        public string Role { get; }
        public string Initials { get; }

        [ObservableProperty] private bool _isSelected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AssignmentStatusText))]
        private bool _isDeployed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AssignmentStatusText))]
        private string _deployedProjectName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AssignmentStatusText))]
        private string _deployedCrewLabel = string.Empty;

        public string AssignmentStatusText => IsDeployed
            ? $"Deployed to {DeployedProjectName} ({DeployedCrewLabel})"
            : "Unassigned";

        public SelectableDailyEmployee(EmployeeSummaryDto dto)
        {
            Id = dto.Id;
            FullName = dto.DisplayName;
            Role = dto.Role.ToString();
            var f = dto.FirstName.FirstOrDefault();
            var l = dto.LastName.FirstOrDefault();
            Initials = $"{f}{l}".ToUpper();
        }
    }
}
