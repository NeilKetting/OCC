using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.Interfaces;
using OCC.Shared.Models;
using OCC.Shared.Utils;
using OCC.WpfClient.Features.ProjectHub.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class CreateProjectViewModel : OverlayViewModel
    {
        private readonly IProjectService _projectService;
        private readonly ICustomerService _customerService;
        private readonly IEmployeeService _employeeService;
        private readonly ISubContractorService _subContractorService;
        private readonly IUserService _userService;
        private readonly IGoogleMapsService _googleMapsService;
        private readonly ISettingsService _settingsService;
        private readonly OCC.WpfClient.Services.Infrastructure.ConnectionSettings _connectionSettings;
        private string _sessionToken = Guid.NewGuid().ToString();
        private System.Threading.CancellationTokenSource? _addressCts;
        private bool _isHandlingSelection;

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
        [ObservableProperty] private int _reconciliationTotalCount;
        [ObservableProperty] private int _reconciliationMapCount;
        [ObservableProperty] private int _reconciliationCreateCount;
        [ObservableProperty] private int _reconciliationSkipCount;
        
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
            OCC.WpfClient.Services.Infrastructure.ConnectionSettings connectionSettings)
        {
            _projectService = projectService;
            _customerService = customerService;
            _employeeService = employeeService;
            _subContractorService = subContractorService;
            _userService = userService;
            _googleMapsService = googleMapsService;
            _settingsService = settingsService;
            _connectionSettings = connectionSettings;

            Title = "Create New Project";

            Project = new ProjectWrapper(new Project 
            { 
                Id = Guid.NewGuid(),
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

        private void UpdateReconciliationStats()
        {
            ReconciliationTotalCount = ReconciliationRows.Count;
            ReconciliationMapCount = ReconciliationRows.Count(r => r.Action == ReconciliationAction.MapToExisting);
            ReconciliationCreateCount = ReconciliationRows.Count(r => r.Action == ReconciliationAction.CreateNew);
            ReconciliationSkipCount = ReconciliationRows.Count(r => r.Action == ReconciliationAction.Skip);
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
                foreach (var e in employees.Where(x => x.Role == EmployeeRole.SiteManager || 
                                                 x.Role == EmployeeRole.SnrForeman || 
                                                 x.Role == EmployeeRole.JnrForeman || 
                                                 x.Role == EmployeeRole.LegacySeniorForeman).OrderBy(x => x.FirstName))
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
                NotifyError("Error", "Failed to load lookup data.");
            }
        }

        private async void Project_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isHandlingSelection) return;

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
                NotifyWarning("Setup Required", "Google Maps API Key is missing. Suggestions will not work.");
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
                foreach (var s in suggestions ?? Array.Empty<AddressSuggestion>()) AddressSuggestions.Add(s);
            }
            catch (OperationCanceledException)
            {
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
                    _isHandlingSelection = true;
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
                    _isHandlingSelection = false;
                }
            }
            catch (Exception)
            {
                NotifyError("Google Maps", "Failed to retrieve address details.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [ObservableProperty] private int _animationPulse;

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
                AnimationPulse = 0;
                await Task.Delay(100);
                AnimationPulse = 1;
                
                var firstError = Project.Errors.FirstOrDefault() ?? "Please correct the errors before saving.";
                NotifyWarning("Validation", firstError);
                return;
            }

            try
            {
                IsBusy = true;

                // Ensure project has an ID before linking tasks
                if (Project.Id == Guid.Empty)
                {
                    Project.Model.Id = Guid.NewGuid();
                }

                // Ensure imported tasks are attached if present
                if (_importedTasks != null && _importedTasks.Any())
                {
                    Project.Model.Tasks = _importedTasks;
                    
                    // Also ensure every task is explicitly linked to the project
                    foreach (var t in _importedTasks)
                    {
                        t.ProjectId = Project.Id;
                    }
                }

                await _projectService.CreateProjectAsync(Project.Model);

                NotifySuccess("Success", $"Project '{Project.Name}' created successfully.");
                Close(Project.Id);
            }
            catch (Exception ex)
            {
                NotifyError("Error", $"Failed to create project: {ex.Message}");
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
                var progress = new Progress<(string msg, double pct)>(p => 
                {
                    ImportProgressMessage = p.msg;
                    WeakReferenceMessenger.Default.Send(new ImportProgressMessage(new ImportProgressInfo 
                    { 
                        Message = p.msg, 
                        Progress = p.pct, 
                        IsVisible = true,
                        IsComplete = p.pct >= 100
                    }));
                });

                var result = await parser.ParseAsync(stream, progress);
                
                if (!string.IsNullOrEmpty(result.ProjectName)) Project.Name = result.ProjectName;
                _importedTasks = result.FlatTasks;

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
            
            // Add internal company options for mapping (e.g. mapping "OCC" in XML to full name)
            potentialMatches.Add(new AssigneeSelectionViewModel { Id = Guid.Empty, Name = "Orange Circle Construction JHB", Role = "Internal", Type = AssigneeType.Staff, Branch = "Jhb" });
            potentialMatches.Add(new AssigneeSelectionViewModel { Id = Guid.Empty, Name = "Orange Circle Construction CPT", Role = "Internal", Type = AssigneeType.Staff, Branch = "CPT" });
            potentialMatches.Add(new AssigneeSelectionViewModel { Id = Guid.Empty, Name = "OCC", Role = "Internal", Type = AssigneeType.Staff, Branch = "Global" });

            foreach (var e in employees) potentialMatches.Add(new AssigneeSelectionViewModel { Id = e.Id, Name = e.DisplayName, Role = e.Role.ToString(), Type = AssigneeType.Staff, Branch = e.Branch });
            foreach (var sc in subContractors) potentialMatches.Add(new AssigneeSelectionViewModel { Id = sc.Id, Name = sc.Name, Role = "Contractor", Type = AssigneeType.Contractor, Branch = sc.Branch });

            foreach (var rName in resourceNames)
            {
                // Prioritize exact or high-confidence matches (including acronyms like OCC)
                var bestMatch = potentialMatches
                    .Select(m => new { Match = m, Score = SimilarityHelper.GetSimilarity(rName, m.Name) })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if (bestMatch != null && bestMatch.Score >= 0.9)
                {
                    ResolveAssignee(rName, bestMatch.Match.Id, bestMatch.Match.Name, bestMatch.Match.Type);
                    continue;
                }

                // If no high-confidence match, look for fuzzy matches for the user to choose from
                var fuzzyMatches = potentialMatches
                    .Select(m => new { Match = m, Score = SimilarityHelper.GetSimilarity(rName, m.Name) })
                    .Where(x => x.Score >= 0.4) // Lowered threshold for broader suggestions
                    .OrderByDescending(x => x.Score)
                    .Take(8)
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

                row.OnActionUpdated = UpdateReconciliationStats;
                ReconciliationRows.Add(row);
            }

            UpdateReconciliationStats();

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
                        var newSub = new SubContractor 
                        { 
                            Name = row.ImportedName, 
                            Branch = string.IsNullOrWhiteSpace(row.Branch) ? (Project.Location ?? "Jhb") : row.Branch,
                            Email = row.Email,
                            Phone = row.Phone,
                            Address = row.Address,
                            Specialties = row.Specialties
                        };
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
                NotifyError("Reconciliation", $"Error during reconciliation: {ex.Message}");
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
            _ = CreateProject();
        }

        [RelayCommand]
        private void CancelImportSave()
        {
            ShowImportComplete = false;
        }

        [RelayCommand]
        private async Task BrowseImport()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MS Project XML (*.xml)|*.xml",
                Title = "Import from MS Project"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var stream = System.IO.File.OpenRead(dialog.FileName);
                    await ImportProjectAsync(stream);
                }
                catch (Exception ex)
                {
                    NotifyError("Import Error", $"Failed to read file: {ex.Message}");
                }
            }
        }
    }
}
