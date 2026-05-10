using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.Shared.Interfaces;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.ModelWrappers;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.ProjectHub.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectEditorViewModel : OverlayViewModel
    {
        private readonly IProjectService _projectService;
        private readonly ICustomerService _customerService;
        private readonly IEmployeeService _employeeService;
        private readonly IGoogleMapsService _googleMapsService;
        private readonly OCC.WpfClient.Services.Infrastructure.ConnectionSettings _connectionSettings;

        private string _sessionToken = Guid.NewGuid().ToString();
        private System.Threading.CancellationTokenSource? _addressCts;

        [ObservableProperty] private ProjectWrapper? _project;
        [ObservableProperty] private Employee? _selectedSiteManager;
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private AddressSuggestion? _selectedAddressSuggestion;
        [ObservableProperty] private bool _isAddressMissing;

        public ObservableCollection<Employee> SiteManagers { get; } = new();
        public ObservableCollection<Customer> Customers { get; } = new();
        public ObservableCollection<AddressSuggestion> AddressSuggestions { get; } = new();
        
        public string[] Statuses { get; } = new[] { "Planning", "In Progress", "On Hold", "Completed" };
        public string[] Priorities { get; } = new[] { "Low", "Medium", "High", "Critical" };

        public ProjectEditorViewModel(
            IProjectService projectService,
            ICustomerService customerService,
            IEmployeeService employeeService,
            IGoogleMapsService googleMapsService,
            OCC.WpfClient.Services.Infrastructure.ConnectionSettings connectionSettings)
        {
            _projectService = projectService;
            _customerService = customerService;
            _employeeService = employeeService;
            _googleMapsService = googleMapsService;
            _connectionSettings = connectionSettings;

            Title = "Edit Project";
        }

        public async Task InitializeAsync(Guid projectId)
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading project details...";

                await LoadLookupDataAsync();

                var model = await _projectService.GetProjectAsync(projectId);
                if (model != null)
                {
                    Project = new ProjectWrapper(model);
                    Project.PropertyChanged += Project_PropertyChanged;
                    
                    SelectedCustomer = Customers.FirstOrDefault(c => c.Id == model.CustomerId);
                    SelectedSiteManager = SiteManagers.FirstOrDefault(e => e.Id == model.SiteManagerId);
                }
            }
            catch (Exception)
            {
                NotifyError("Error", "Failed to load project details.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadLookupDataAsync()
        {
            Customers.Clear();
            var customers = await _customerService.GetCustomerSummariesAsync();
            foreach (var c in customers.OrderBy(x => x.Name))
            {
                Customers.Add(new Customer { Id = c.Id, Name = c.Name });
            }

            SiteManagers.Clear();
            var employees = await _employeeService.GetEmployeesAsync();
            foreach (var e in employees.Where(x => x.Role == EmployeeRole.SiteManager).OrderBy(x => x.FirstName))
            {
                SiteManagers.Add(new Employee { Id = e.Id, FirstName = e.FirstName, LastName = e.LastName, EmployeeNumber = e.EmployeeNumber });
            }
        }

        private async void Project_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectWrapper.StreetLine1))
            {
                await UpdateAddressSuggestions();
            }
        }

        private async Task UpdateAddressSuggestions()
        {
            if (Project == null || (SelectedAddressSuggestion != null && Project.StreetLine1 == SelectedAddressSuggestion.Description))
                return;

            if (string.IsNullOrWhiteSpace(Project.StreetLine1) || Project.StreetLine1.Length < 3)
            {
                AddressSuggestions.Clear();
                return;
            }

            _addressCts?.Cancel();
            _addressCts = new System.Threading.CancellationTokenSource();
            var token = _addressCts.Token;

            try
            {
                await Task.Delay(300, token);
                var suggestions = await _googleMapsService.GetAddressSuggestionsAsync(Project.StreetLine1, _sessionToken);
                if (token.IsCancellationRequested) return;

                AddressSuggestions.Clear();
                foreach (var s in suggestions ?? Array.Empty<AddressSuggestion>()) AddressSuggestions.Add(s);
            }
            catch { }
        }

        partial void OnSelectedAddressSuggestionChanged(AddressSuggestion? value)
        {
            if (value != null) _ = HandleAddressSelection(value);
        }

        private async Task HandleAddressSelection(AddressSuggestion suggestion)
        {
            try
            {
                IsBusy = true;
                var details = await _googleMapsService.GetPlaceDetailsAsync(suggestion.PlaceId, _sessionToken);
                if (details != null && Project != null)
                {
                    Project.StreetLine1 = details.StreetLine1;
                    Project.StreetLine2 = details.StreetLine2;
                    Project.City = details.City;
                    Project.StateOrProvince = details.StateOrProvince;
                    Project.PostalCode = details.PostalCode;
                    Project.Country = details.Country;
                    Project.Latitude = details.Latitude;
                    Project.Longitude = details.Longitude;
                    
                    AddressSuggestions.Clear();
                    SelectedAddressSuggestion = null;
                    _sessionToken = Guid.NewGuid().ToString();
                }
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SaveProject()
        {
            if (Project == null) return;

            Project.CustomerId = SelectedCustomer?.Id;
            Project.Customer = SelectedCustomer?.Name ?? string.Empty;
            Project.SiteManagerId = SelectedSiteManager?.Id;

            Project.Validate(ProjectCreationMode.Comprehensive);

            if (Project.HasValidationErrors)
            {
                NotifyWarning("Validation", Project.Errors.FirstOrDefault() ?? "Please fix errors.");
                return;
            }

            try
            {
                IsBusy = true;
                await _projectService.UpdateProjectAsync(Project.Model);
                NotifySuccess("Success", "Project updated successfully.");
                Close(true);
            }
            catch (Exception)
            {
                NotifyError("Error", "Failed to update project.");
            }
            finally { IsBusy = false; }
        }
    }
}
