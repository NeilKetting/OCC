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
        private readonly ISubContractorService _subContractorService;
        private readonly IUserService _userService;
        private readonly IGoogleMapsService _googleMapsService;
        private readonly ISettingsService _settingsService;
        private readonly IToastService _toastService;
        private readonly OCC.WpfClient.Services.Infrastructure.ConnectionSettings _connectionSettings;
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
        [ObservableProperty] private bool _isReconciling;
        public ObservableCollection<AssigneeReconciliationRow> ReconciliationRows { get; } = new();

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
            ISubContractorService subContractorService,
            ISettingsService settingsService,
            IToastService toastService,
            OCC.WpfClient.Services.Infrastructure.ConnectionSettings connectionSettings)
        {
            _projectService = projectService;
            _customerService = customerService;
            _employeeService = employeeService;
            _subContractorService = subContractorService;
            _userService = userService;
            _googleMapsService = googleMapsService;
            _settingsService = settingsService;
            _toastService = toastService;
            _connectionSettings = connectionSettings;

            Project = new ProjectWrapper(new Project 
            { 
                Status = "Planning", 
                Priority = "Medium", 
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(1),
                ProjectManager = "Neil Ketting"
            });

            _ = LoadDataAsync();
        }

        partial void OnProjectChanged(ProjectWrapper value)
        {
            if (value != null)
            {
                value.PropertyChanged += Project_PropertyChanged;
            }
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
            catch (Exception)
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

            if (string.IsNullOrWhiteSpace(_connectionSettings.GoogleApiKey))
            {
                _toastService.ShowWarning("Setup Required", "Google Maps API Key is missing. Suggestions will not work.");
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

                if (suggestions == null || !suggestions.Any())
                {
                    System.Diagnostics.Debug.WriteLine("[CreateProjectViewModel] No suggestions returned. Verify API Key.");
                }

                AddressSuggestions.Clear();
                foreach (var s in suggestions ?? Array.Empty<AddressSuggestion>()) AddressSuggestions.Add(s);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateProjectViewModel] Address Search Error");
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
            catch (Exception)
            {
                _toastService.ShowError("Google Maps", "Failed to retrieve address details.");
                System.Diagnostics.Debug.WriteLine($"[CreateProjectViewModel] Place Details Error");
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
            catch (Exception)
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

                await MatchAssigneesAsync(result.Resources);
            }
            catch (Exception)
            {
                ImportProgressMessage = "Error occurred during import.";
            }
            finally
            {
                IsImporting = false;
            }
        }

        private async Task MatchAssigneesAsync(List<string> resourceNames)
        {
            ReconciliationRows.Clear();
            if (resourceNames == null || !resourceNames.Any())
            {
                ImportProgressMessage = "Import Complete!";
                ShowImportComplete = true;
                return;
            }

            ImportProgressMessage = "Matching assignees...";
            
            var employees = await _employeeService.GetEmployeesAsync();
            var subContractors = await _subContractorService.GetSubContractorSummariesAsync();

            var potentialMatches = new List<AssigneeSelectionViewModel>();
            foreach (var e in employees) potentialMatches.Add(new AssigneeSelectionViewModel { Id = e.Id, Name = e.DisplayName, Role = e.Role.ToString(), Type = AssigneeType.Staff, Branch = e.Branch });
            foreach (var sc in subContractors) potentialMatches.Add(new AssigneeSelectionViewModel { Id = sc.Id, Name = sc.Name, Role = "Contractor", Type = AssigneeType.Contractor, Branch = sc.Branch });

            foreach (var rName in resourceNames)
            {
                // Exact Match check
                var exact = potentialMatches.FirstOrDefault(m => string.Equals(m.Name, rName, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    // Update imported tasks directly
                    ResolveAssignee(rName, exact.Id, exact.Name, exact.Type);
                    continue;
                }

                // Fuzzy Match check
                var fuzzyMatches = potentialMatches
                    .Select(m => new { Match = m, Score = SimilarityHelper.GetSimilarity(rName, m.Name) })
                    .Where(x => x.Score >= 0.8)
                    .OrderByDescending(x => x.Score)
                    .Take(5)
                    .ToList();

                var row = new AssigneeReconciliationRow { ImportedName = rName };
                foreach (var f in fuzzyMatches) row.SuggestedMatches.Add(f.Match);

                if (fuzzyMatches.Any())
                {
                    row.Action = ReconciliationAction.MapToExisting;
                    row.SelectedMatch = fuzzyMatches.First().Match;
                }
                else
                {
                    row.Action = ReconciliationAction.CreateNew;
                }

                ReconciliationRows.Add(row);
            }

            if (ReconciliationRows.Any())
            {
                IsReconciling = true;
            }
            else
            {
                ImportProgressMessage = "Import Complete!";
                ShowImportComplete = true;
            }
        }

        private void ResolveAssignee(string importedName, Guid assigneeId, string resolvedName, AssigneeType type)
        {
            if (_importedTasks == null) return;

            foreach (var task in _importedTasks)
            {
                foreach (var assignment in task.Assignments.Where(a => a.AssigneeName == importedName))
                {
                    assignment.AssigneeId = assigneeId;
                    assignment.AssigneeName = resolvedName;
                    assignment.AssigneeType = type;
                }
            }
        }

        [RelayCommand]
        private async Task ConfirmReconciliation()
        {
            IsBusy = true;
            BusyText = "Applying reconciliation...";

            try
            {
                foreach (var row in ReconciliationRows)
                {
                    if (row.Action == ReconciliationAction.Skip) continue;

                    if (row.Action == ReconciliationAction.CreateNew)
                    {
                        var newSub = new SubContractor { Name = row.ImportedName, Branch = Project.Location ?? "Jhb" };
                        var created = await _subContractorService.CreateSubContractorAsync(newSub);
                        ResolveAssignee(row.ImportedName, created.Id, created.Name, AssigneeType.Contractor);
                    }
                    else if (row.Action == ReconciliationAction.MapToExisting && row.SelectedMatch != null)
                    {
                        ResolveAssignee(row.ImportedName, row.SelectedMatch.Id, row.SelectedMatch.Name, row.SelectedMatch.Type);
                    }
                }

                IsReconciling = false;
                ImportProgressMessage = "Import Complete!";
                ShowImportComplete = true;
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Reconciliation", $"Error during reconciliation: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
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
