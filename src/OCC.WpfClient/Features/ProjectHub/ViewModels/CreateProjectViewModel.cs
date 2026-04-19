using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.Interfaces;
using OCC.Shared.Models;
using OCC.Shared.Utils;
using OCC.WpfClient.Features.ProjectHub.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    // Enum moved to Models namespace to avoid circular dependencies

    public partial class CreateProjectViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly ICustomerService _customerService;
        private readonly IEmployeeService _employeeService;
        private readonly IUserService _userService;
        private readonly IGoogleMapsService _googleMapsService;
        private readonly ISettingsService _settingsService;
        private readonly IToastService _toastService;
        private string _sessionToken = Guid.NewGuid().ToString();
        private System.Threading.CancellationTokenSource? _addressCts;

        public event EventHandler? CloseRequested;
        public event EventHandler<Guid>? ProjectCreated;

        [ObservableProperty] private ProjectWrapper _project;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsComprehensiveMode))]
        private ProjectCreationMode _creationMode = ProjectCreationMode.Quick;

        public bool IsComprehensiveMode => CreationMode == ProjectCreationMode.Comprehensive;

        [ObservableProperty] private Employee? _selectedSiteManager;
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private bool _isImporting;
        [ObservableProperty] private string _importProgressMessage = string.Empty;
        [ObservableProperty] private bool _showImportComplete;
        [ObservableProperty] private bool _isAddressMissing = true;
        [ObservableProperty] private string _validationMessage = "Geofencing requires a site address.";
        [ObservableProperty] private AddressSuggestion? _selectedAddressSuggestion;

        public ObservableCollection<Employee> SiteManagers { get; } = new();
        public ObservableCollection<Customer> Customers { get; } = new();
        public ObservableCollection<AddressSuggestion> AddressSuggestions { get; } = new();
        public string[] ProjectManagers { get; } = new[] { "Neil Ketting", "John Doe", "Jane Smith" };
        public string[] Statuses { get; } = new[] { "Planning", "In Progress", "On Hold", "Completed" };
        public string[] Priorities { get; } = new[] { "Low", "Medium", "High", "Critical" };

        private List<ProjectTask>? _importedTasks;

        public CreateProjectViewModel(
            IProjectService projectService,
            ICustomerService customerService,
            IEmployeeService employeeService,
            IUserService userService,
            IGoogleMapsService googleMapsService,
            ISettingsService settingsService,
            IToastService toastService)
        {
            _projectService = projectService;
            _customerService = customerService;
            _employeeService = employeeService;
            _userService = userService;
            _googleMapsService = googleMapsService;
            _settingsService = settingsService;
            _toastService = toastService;

            _project = new ProjectWrapper(new Project 
            { 
                Status = "Planning", 
                Priority = "Medium", 
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(1),
                ProjectManager = "Neil Ketting"
            });

            Project.PropertyChanged += Project_PropertyChanged;
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var customers = await _customerService.GetCustomerSummariesAsync();
                foreach (var c in customers.OrderBy(x => x.Name))
                {
                    var cust = new Customer
                    {
                        Id = c.Id,
                        Name = c.Name
                    };
                    Customers.Add(cust);
                }

                var employees = await _employeeService.GetEmployeesAsync();
                foreach (var e in employees.Where(x => x.Role == EmployeeRole.SiteManager).OrderBy(x => x.FirstName))
                {
                    var emp = new Employee
                    {
                        Id = e.Id,
                        FirstName = e.FirstName,
                        LastName = e.LastName,
                        EmployeeNumber = e.EmployeeNumber
                    };
                    SiteManagers.Add(emp);
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Failed to load lookup data.");
            }
        }

        private async void Project_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectWrapper.StreetLine1))
            {
                await UpdateAddressSuggestions();
            }

            if (e.PropertyName == nameof(ProjectWrapper.Latitude) || e.PropertyName == nameof(ProjectWrapper.Longitude))
            {
                if (Project.Latitude.HasValue && Project.Longitude.HasValue)
                {
                    IsAddressMissing = false;
                    ValidationMessage = string.Empty;
                }
            }
        }

        private async Task UpdateAddressSuggestions()
        {
            if (SelectedAddressSuggestion != null && Project.StreetLine1 == SelectedAddressSuggestion.Description)
                return;

            if (string.IsNullOrWhiteSpace(Project.StreetLine1) || Project.StreetLine1.Length < 3)
            {
                AddressSuggestions.Clear();
                return;
            }

            // Debounce logic
            _addressCts?.Cancel();
            _addressCts = new System.Threading.CancellationTokenSource();
            var token = _addressCts.Token;

            try
            {
                await Task.Delay(300, token);
                
                var suggestions = await _googleMapsService.GetAddressSuggestionsAsync(Project.StreetLine1, _sessionToken);
                
                if (token.IsCancellationRequested) return;

                AddressSuggestions.Clear();
                foreach (var s in suggestions) AddressSuggestions.Add(s);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateProjectViewModel] Address Search Error: {ex.Message}");
            }
        }

        partial void OnSelectedAddressSuggestionChanged(AddressSuggestion? value)
        {
            if (value != null)
            {
                _ = HandleAddressSelection(value);
            }
        }

        private async Task HandleAddressSelection(AddressSuggestion suggestion)
        {
            if (suggestion == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Fetching address details...";
                
                var details = await _googleMapsService.GetPlaceDetailsAsync(suggestion.PlaceId, _sessionToken);
                if (details != null)
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
            catch (Exception ex)
            {
                _toastService.ShowError("Google Maps", "Failed to retrieve address details.");
                System.Diagnostics.Debug.WriteLine($"[CreateProjectViewModel] Place Details Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [ObservableProperty] private int _animationPulse;
        [ObservableProperty] private bool _showValidationSummary;

        [RelayCommand]
        private async Task CreateProject()
        {
            // Sync VM selection to Model before validation
            Project.CustomerId = SelectedCustomer?.Id;
            Project.Customer = SelectedCustomer?.Name ?? string.Empty;
            Project.SiteManagerId = SelectedSiteManager?.Id;

            Project.Validate(CreationMode);

            if (Project.HasValidationErrors)
            {
                // Pulse the animation signal
                AnimationPulse = 0;
                await Task.Delay(100); // Give dispatcher time to process the reset
                AnimationPulse = 1;
                
                var firstError = Project.Errors.FirstOrDefault() ?? "Please correct the errors before saving.";
                _toastService.ShowWarning("Validation", firstError);
                return;
            }

            try
            {
                IsBusy = true;
                await _projectService.CreateProjectAsync(Project.Model);

                _toastService.ShowSuccess("Success", $"Project '{Project.Name}' created successfully.");
                ProjectCreated?.Invoke(this, Project.Id);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Failed to create project.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task ImportProjectAsync(System.IO.Stream stream)
        {
            IsImporting = true;
            ImportProgressMessage = "Starting import...";
            ShowImportComplete = false;

            try
            {
                var parser = new MSProjectXmlParser();
                var progress = new Progress<string>(msg => ImportProgressMessage = msg);

                var result = await parser.ParseAsync(stream, progress);
                
                if (!string.IsNullOrEmpty(result.ProjectName)) Project.Name = result.ProjectName;
                _importedTasks = result.Tasks;
                
                ImportProgressMessage = "Import Complete!";
                ShowImportComplete = true;
            }
            catch (Exception ex)
            {
                ImportProgressMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsImporting = false;
            }
        }

        [RelayCommand]
        private void ConfirmImportSave()
        {
            ShowImportComplete = false;
            CreateProjectCommand.Execute(null);
        }

        [RelayCommand]
        private void CancelImportSave()
        {
            ShowImportComplete = false;
        }

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void BrowseImport()
        {
            // TODO: Use DialogService to open file picker for XML
            _toastService.ShowInfo("Import", "Please select an MS Project XML file.");
        }
    }
}
