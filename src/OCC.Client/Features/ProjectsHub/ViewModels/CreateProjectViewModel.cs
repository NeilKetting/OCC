using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ModelWrappers;
using OCC.Shared.Interfaces;
using OCC.Shared.Services;
using OCC.Shared.Utils;
using OCC.Client.Infrastructure;
using OCC.Client.Messages;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public enum ProjectCreationMode
    {
        Quick,
        Comprehensive
    }

    public partial class CreateProjectViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Customer> _customerRepository;
        private readonly Services.Interfaces.ICustomerService _customerService;
        private readonly Services.Interfaces.IDialogService _dialogService;
        private readonly IRepository<ProjectTask> _taskRepository;
        private readonly IRepository<AppSetting> _appSettingsRepository;
        private readonly IRepository<Employee> _staffRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IGoogleMapsService _googleMapsService;
        private string _sessionToken = Guid.NewGuid().ToString();

        #endregion

        #region Events

        public event EventHandler? CloseRequested;
        public event EventHandler<Guid>? ProjectCreated;

        #endregion

        #region Observables

        [ObservableProperty]
        private ProjectWrapper _project;

        // UI State
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ModalWidth))]
        [NotifyPropertyChangedFor(nameof(IsSettingsVisible))]
        private ProjectCreationMode _creationMode = ProjectCreationMode.Quick;

        public bool IsSettingsVisible => CreationMode == ProjectCreationMode.Comprehensive;

        [ObservableProperty]
        private bool _isJustMe = true;

        [ObservableProperty]
        private bool _isEveryone = false;

        [ObservableProperty]
        private bool _isSpecific = false;

        [ObservableProperty]
        private bool _isTemplate = false;

        [ObservableProperty]
        private Employee? _siteManager;

        [ObservableProperty]
        private Customer? _customer;

        // Import State
        [ObservableProperty]
        private bool _isImporting;

        [ObservableProperty]
        private string _importProgressMessage = string.Empty;

        [ObservableProperty]
        private bool _showImportComplete;

        [ObservableProperty]
        private bool _isAddressMissing;

        [ObservableProperty]
        private string _validationMessage = string.Empty;

        [ObservableProperty]
        private AddressSuggestion? _selectedAddressSuggestion;

        [ObservableProperty]
        private bool _isAddCustomerPopupVisible;

        [ObservableProperty]
        private OCC.Client.Features.CustomerHub.ViewModels.CustomerDetailViewModel? _customerDetailPopup;

        private List<ProjectTask>? _importedTasks;

        #endregion

        #region Properties

        public double ModalWidth => IsSettingsVisible ? 1100 : 700;
        
        // Collections
        public System.Collections.ObjectModel.ObservableCollection<string> ProjectManagers { get; } = new();

        public System.Collections.ObjectModel.ObservableCollection<Employee> SiteManagers { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<Customer> Customers { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<AddressSuggestion> AddressSuggestions { get; } = new();
        public string[] Statuses { get; } = new[] { "Planning", "In Progress", "On Hold", "Completed" };
        public string[] Priorities { get; } = new[] { "low", "Medium", "Important", "Critical" };

        #endregion

        #region Constructors

        public CreateProjectViewModel()
        {
            // Parameterless constructor for design-time support
            _projectRepository = null!;
            _customerRepository = null!;
            _taskRepository = null!;
            _appSettingsRepository = null!;
            _staffRepository = null!;
            _userRepository = null!;
            _googleMapsService = null!;
            _customerService = null!;
            _dialogService = null!;
            _project = new ProjectWrapper(new Project());
        }
        
        public CreateProjectViewModel(
            IRepository<Project> projectRepository, 
            IRepository<Customer> customerRepository, 
            IRepository<ProjectTask> taskRepository,
            IRepository<AppSetting> appSettingsRepository,
            IRepository<Employee> staffRepository,
            IRepository<User> userRepository,
            IGoogleMapsService googleMapsService,
            Services.Interfaces.ICustomerService customerService,
            Services.Interfaces.IDialogService dialogService)
        {
            _projectRepository = projectRepository;
            _customerRepository = customerRepository;
            _taskRepository = taskRepository;
            _appSettingsRepository = appSettingsRepository;
            _staffRepository = staffRepository;
            _userRepository = userRepository;
            _googleMapsService = googleMapsService;
            _customerService = customerService;
            _dialogService = dialogService;
            Project = new ProjectWrapper(new Project());
            
            LoadCustomers();
            LoadManagers();

            // Trigger geofencing warning on start
            IsAddressMissing = true;
            ValidationMessage = "Geofencing requires a site address. Please search for and select the project location.";

            Project.PropertyChanged += Project_PropertyChanged;
        }

        private async void Project_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectWrapper.StreetLine1))
            {
                // Only update suggestions if this wasn't a manual change from a selection
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

        partial void OnSelectedAddressSuggestionChanged(AddressSuggestion? value)
        {
            if (value != null)
            {
                _ = HandleAddressSelection(value);
            }
        }

        private async Task UpdateAddressSuggestions()
        {
            // If we're currently processing a selection, don't update suggestions
            if (SelectedAddressSuggestion != null && Project.StreetLine1 == SelectedAddressSuggestion.Description)
                return;

            if (string.IsNullOrWhiteSpace(Project.StreetLine1) || Project.StreetLine1.Length < 3)
            {
                AddressSuggestions.Clear();
                return;
            }

            var suggestions = await _googleMapsService.GetAddressSuggestionsAsync(Project.StreetLine1, _sessionToken);
            AddressSuggestions.Clear();
            foreach (var s in suggestions) AddressSuggestions.Add(s);
        }

        public async Task HandleAddressSelection(AddressSuggestion suggestion)
        {
            if (suggestion == null) return;

            System.Diagnostics.Debug.WriteLine($"[CreateProjectViewModel] Handling selection: {suggestion.Description}");

            var details = await _googleMapsService.GetPlaceDetailsAsync(suggestion.PlaceId, _sessionToken);
            if (details != null)
            {
                // Set the fields. We don't need a delay if we're careful about the property change logic.
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

                // Reset session token for next address search
                _sessionToken = Guid.NewGuid().ToString();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[CreateProjectViewModel] Failed to get place details");
            }
        }

        partial void OnSiteManagerChanged(Employee? value)
        {
            Project.SiteManagerId = value?.Id;
        }

        partial void OnCustomerChanged(Customer? value)
        {
            Project.CustomerId = value?.Id;
            Project.Customer = value?.Name ?? string.Empty;
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task CreateProject()
        {
            Project.Validate();
            if (Project.HasErrors)
            {
                IsAddressMissing = true;
                ValidationMessage = "Please fix the validation errors before saving.";
                CreationMode = ProjectCreationMode.Comprehensive;
                return;
            }

            IsAddressMissing = false;
            ValidationMessage = string.Empty;

            Project.CommitToModel();
            var newProject = Project.Model;

            if (string.IsNullOrWhiteSpace(newProject.ShortName))
            {
                newProject.ShortName = newProject.Name.Substring(0, Math.Min(3, newProject.Name.Length)).ToUpper();
            }

            // Map UI selection to model if not already set (ProjectWrapper properties)
            newProject.SiteManagerId = Project.SiteManagerId;
            newProject.Customer = Project.Customer;
            newProject.CustomerId = Project.CustomerId;

            // Snapshot Global Work Hours
            try 
            {
                 var settings = await _appSettingsRepository.GetAllAsync();
                 var start = settings.FirstOrDefault(s => s.Key == "WorkStartTime");
                 var end = settings.FirstOrDefault(s => s.Key == "WorkEndTime");
                 var lunch = settings.FirstOrDefault(s => s.Key == "LunchDurationMinutes");

                 if (start != null && TimeSpan.TryParse(start.Value, out var sVal)) newProject.WorkStartTime = sVal;
                 if (end != null && TimeSpan.TryParse(end.Value, out var eVal)) newProject.WorkEndTime = eVal;
                 if (lunch != null && int.TryParse(lunch.Value, out var lVal)) newProject.LunchDurationMinutes = lVal;
            }
            catch (Exception)
            {
                 // CreateProject logic shouldn't fail if settings are missing, defaults apply
            }

            await _projectRepository.AddAsync(newProject);
            
            // Save imported tasks if any
            if (_importedTasks != null && _importedTasks.Count > 0)
            {
                // 1. Flatten and Properties Pass
                var allTasks = new List<ProjectTask>();
                foreach (var rootTask in _importedTasks)
                {
                    FlattenTasks(rootTask, allTasks, 0);
                }

                int orderCounter = 0;
                foreach (var task in allTasks)
                {
                    task.OrderIndex = orderCounter++;
                    task.ProjectId = newProject.Id;
                    // Ensure Id is set
                    if (task.Id == Guid.Empty) task.Id = Guid.NewGuid();
                }

                // 2. Save Pass - Only save Roots, EF Core adds children automatically
                foreach (var rootTask in _importedTasks)
                {
                    await _taskRepository.AddAsync(rootTask);
                }
            }

            ProjectCreated?.Invoke(this, newProject.Id);
            WeakReferenceMessenger.Default.Send(new ProjectCreatedMessage(newProject));
            WeakReferenceMessenger.Default.Send(new NavigationRequestMessage(NavigationRoutes.Projects));
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        
        [RelayCommand]
        private void ConfirmImportSave()
        {
            ShowImportComplete = false;
            
            // If we have coordinates, we can proceed to save
            if (Project.Latitude.HasValue && Project.Longitude.HasValue)
            {
                CreateProjectCommand.Execute(null);
            }
            else
            {
                // If missing coordinates, force "Review" mode to show the address form
                IsAddressMissing = true;
                ValidationMessage = "Project imported, but geofencing requires a site address. Please search for and select the project location.";
                CreationMode = ProjectCreationMode.Comprehensive;
            }
        }

        [RelayCommand]
        private void CancelImportSave()
        {
            ShowImportComplete = false;
            // Keep the data in the form but don't save yet
        }

        [RelayCommand]
        private void StartTemplate()
        {
            // Placeholder
        }


        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void AddCustomer()
        {
            var vm = new OCC.Client.Features.CustomerHub.ViewModels.CustomerDetailViewModel(_customerService, _dialogService);
            vm.InitializeNew();

            vm.CloseRequested += (s, e) => CloseCustomerPopup();
            vm.Saved += (s, e) => 
            {
                var newCustomerName = vm.Name;
                CloseCustomerPopup();
                LoadCustomers(newCustomerName);
            };

            CustomerDetailPopup = vm;
            IsAddCustomerPopupVisible = true;
        }

        private void CloseCustomerPopup()
        {
            IsAddCustomerPopupVisible = false;
            CustomerDetailPopup = null;
        }

        #endregion

        #region Methods

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
                
                if (!string.IsNullOrEmpty(result.ProjectName))
                {
                    Project.Name = result.ProjectName;
                }

                if (result.Tasks.Count > 0)
                {
                    // Logic to set dates from tasks if needed
                }

                _importedTasks = result.Tasks;
                
                ImportProgressMessage = "Import Complete!";
                
                if (!Project.Latitude.HasValue || !Project.Longitude.HasValue)
                {
                    IsAddressMissing = true;
                    ValidationMessage = "Project imported successfully, but please select the site address to enable geofencing.";
                    CreationMode = ProjectCreationMode.Comprehensive;
                }

                await Task.Delay(500); // UI delay
                Project.Validate(); // Show validation errors in results
                ShowImportComplete = true;
            }
            catch (Exception ex)
            {
                ImportProgressMessage = $"Error: {ex.Message}";
                await Task.Delay(2000);
            }
            finally
            {
                IsImporting = false;
            }
        }

        private async void LoadManagers()
        {
            try
            {
                var employees = await _staffRepository.GetAllAsync();
                var users = await _userRepository.GetAllAsync();

                // 1. Load Project Managers 
                // Filter: 'Office' Skill AND 'Admin' Role via LinkedUser
                var adminUsers = users.Where(u => u.UserRole == UserRole.Admin).Select(u => u.Id).ToHashSet();
                
                var projectManagers = employees
                    .Where(e => e.Role == EmployeeRole.Office && e.LinkedUserId.HasValue && adminUsers.Contains(e.LinkedUserId.Value))
                    .OrderBy(e => e.FirstName)
                    .Select(e => e.DisplayName);

                ProjectManagers.Clear();
                foreach(var pm in projectManagers)
                {
                    ProjectManagers.Add(pm);
                }

                // Default selection
                if (ProjectManagers.Count > 0)
                    Project.ProjectManager = ProjectManagers[0];


                // 2. Load Site Managers
                // Broadened for testing: Show all employees with SiteManager role/skill
                var siteManagers = employees
                    .Where(e => e.Role == EmployeeRole.SiteManager)
                    .OrderBy(e => e.FirstName);

                SiteManagers.Clear();
                foreach (var sm in siteManagers)
                {
                    SiteManagers.Add(sm);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading managers: {ex.Message}");
            }
        }

        private async void LoadCustomers(string? customerToSelect = null)
        {
            try 
            {
                var customers = await _customerRepository.GetAllAsync();
                Customers.Clear();
                foreach (var c in customers.OrderBy(c => c.Name))
                {
                    Customers.Add(c);
                }

                if (!string.IsNullOrEmpty(customerToSelect))
                {
                    var found = Customers.FirstOrDefault(c => c.Name.Equals(customerToSelect, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        Customer = found;
                    }
                }
            }
            catch(Exception) { }
        }

        #endregion

        #region Helper Methods

        private void FlattenTasks(ProjectTask task, List<ProjectTask> flatList, int level)
        {
            task.IndentLevel = level;
            task.IsGroup = task.Children != null && task.Children.Count > 0;
            
            flatList.Add(task);
            if (task.Children != null && task.Children.Count > 0)
            {
                foreach (var child in task.Children)
                {
                    FlattenTasks(child, flatList, level + 1);
                }

                // Recalculate Summary Dates (Post-Order) to ensure visual containment
                var minStart = DateTime.MaxValue;
                var maxFinish = DateTime.MinValue;
                bool hasDates = false;

                foreach (var child in task.Children)
                {
                     if (child.StartDate < minStart) minStart = child.StartDate;
                     if (child.FinishDate > maxFinish) maxFinish = child.FinishDate;
                     hasDates = true;
                }

                if (hasDates && minStart != DateTime.MaxValue && maxFinish != DateTime.MinValue)
                {
                    task.StartDate = minStart;
                    task.FinishDate = maxFinish;
                    
                    // Update Duration display if needed
                    var days = (maxFinish - minStart).TotalDays;
                    task.Duration = $"{days:0.##} days";
                }
            }
        }

        #endregion
    }
}




