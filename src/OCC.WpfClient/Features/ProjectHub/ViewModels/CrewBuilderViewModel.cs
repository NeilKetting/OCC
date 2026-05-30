using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.DTOs;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    /// <summary>
    /// Overlay ViewModel for the quick crew builder dialog.
    /// Loads today's auto-clocked-in employees, allows multi-select, and
    /// submits a CreateSiteDeploymentRequest to the API.
    /// </summary>
    public partial class CrewBuilderViewModel : OverlayViewModel
    {
        private readonly ICrewDeploymentService _crewService;

        [ObservableProperty] private Guid _projectId;
        [ObservableProperty] private string _projectName = string.Empty;
        [ObservableProperty] private DateTime _deploymentDate;
        [ObservableProperty] private string _crewLabel = string.Empty;
        [ObservableProperty] private ObservableCollection<SelectableEmployee> _availableEmployees = new();
        [ObservableProperty] private bool _isLoadingEmployees;
        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private string _validationMessage = string.Empty;

        private System.Collections.Generic.List<SelectableEmployee> _allEmployees = new();

        public int SelectedCount => AvailableEmployees.Count(e => e.IsSelected);

        public CrewBuilderViewModel(ICrewDeploymentService crewService)
        {
            _crewService = crewService;
            Title = "Build Crew";
        }

        public void Initialize(Guid projectId, string projectName, DateTime date)
        {
            ProjectId = projectId;
            ProjectName = projectName;
            DeploymentDate = date;
            CrewLabel = $"Crew — {date:dd MMM yyyy}";
            _ = LoadEmployeesAsync();
        }

        [RelayCommand]
        private async Task LoadEmployeesAsync()
        {
            try
            {
                IsLoadingEmployees = true;
                var employees = await _crewService.GetTodayClockedInAsync();
                _allEmployees = employees
                    .Select(e => new SelectableEmployee(e))
                    .ToList();

                foreach (var emp in _allEmployees)
                    emp.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SelectedCount));

                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CrewBuilderViewModel.LoadEmployees: {ex.Message}");
            }
            finally
            {
                IsLoadingEmployees = false;
            }
        }

        partial void OnSearchQueryChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            IEnumerable<SelectableEmployee> filtered = _allEmployees;
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLower();
                filtered = filtered.Where(e =>
                    e.FullName.ToLower().Contains(q) ||
                    e.Role.ToLower().Contains(q));
            }
            AvailableEmployees = new ObservableCollection<SelectableEmployee>(filtered);
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var e in _allEmployees) e.IsSelected = true;
            ApplyFilter();
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var e in _allEmployees) e.IsSelected = false;
            ApplyFilter();
        }

        [RelayCommand]
        private async Task SendToSite()
        {
            ValidationMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(CrewLabel))
            {
                ValidationMessage = "Please enter a crew label (e.g. 'Tilers — Morning').";
                return;
            }

            var selected = _allEmployees.Where(e => e.IsSelected).ToList();
            if (!selected.Any())
            {
                ValidationMessage = "Please select at least one employee for this crew.";
                return;
            }

            try
            {
                IsBusy = true;
                BusyText = "Sending crew to site...";

                var request = new CreateSiteDeploymentRequest
                {
                    ProjectId = ProjectId,
                    DeploymentDate = DeploymentDate,
                    Label = CrewLabel,
                    MemberEmployeeIds = selected.Select(e => e.Id).ToList()
                };

                var created = await _crewService.CreateDeploymentAsync(request);
                if (created != null)
                {
                    Close(created);
                }
                else
                {
                    ValidationMessage = "Failed to create crew. Please check your connection and try again.";
                }
            }
            catch (Exception ex)
            {
                ValidationMessage = "An error occurred while sending the crew.";
                System.Diagnostics.Debug.WriteLine($"CrewBuilderViewModel.SendToSite: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel() => Close(null);
    }

    /// <summary>
    /// Wraps an EmployeeSummaryDto with a selectable flag for the crew builder list.
    /// </summary>
    public partial class SelectableEmployee : ObservableObject
    {
        public Guid Id { get; }
        public string FullName { get; }
        public string Role { get; }
        public string Initials { get; }

        [ObservableProperty] private bool _isSelected;

        public SelectableEmployee(EmployeeSummaryDto dto)
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
