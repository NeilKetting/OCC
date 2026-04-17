using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.Client.Services;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ModelWrappers;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Interfaces;
using OCC.Shared.Services;
using System.Linq;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class EditProjectViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Employee> _staffRepository;
        private readonly IRepository<Customer> _customerRepository;
        private readonly IToastService _toastService;
        private readonly IGoogleMapsService _googleMapsService;
        private string _sessionToken = Guid.NewGuid().ToString();

        #endregion

        #region Events

        public event EventHandler? CloseRequested;
        public event EventHandler? ProjectUpdated;

        #endregion

        #region Observables

        [ObservableProperty]
        private ProjectWrapper _project = null!;

        [ObservableProperty]
        private Employee? _siteManager;

        [ObservableProperty]
        private Customer? _customer;

        [ObservableProperty]
        private string _validationMessage = string.Empty;

        [ObservableProperty]
        private bool _isErrorVisible;

        [ObservableProperty]
        private AddressSuggestion? _selectedAddressSuggestion;

        #endregion

        #region Collections

        public string[] ProjectManagers { get; } = new[] { "Origize63@Gmail.Com (Owner)", "John Doe", "Jane Smith" };
        public System.Collections.ObjectModel.ObservableCollection<Employee> SiteManagers { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<Customer> Customers { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<AddressSuggestion> AddressSuggestions { get; } = new();
        public string[] Statuses { get; } = new[] { "Planning", "In Progress", "On Hold", "Completed" };
        public string[] Priorities { get; } = new[] { "low", "Medium", "Important", "Critical" };

        #endregion

        #region Constructor

        public EditProjectViewModel()
        {
            _projectRepository = null!;
            _staffRepository = null!;
            _customerRepository = null!;
            _toastService = null!;
            _googleMapsService = null!;
        }

        public EditProjectViewModel(
            IRepository<Project> projectRepository,
            IRepository<Employee> staffRepository,
            IRepository<Customer> customerRepository,
            IToastService toastService,
            IGoogleMapsService googleMapsService)
        {
            _projectRepository = projectRepository;
            _staffRepository = staffRepository;
            _customerRepository = customerRepository;
            _toastService = toastService;
            _googleMapsService = googleMapsService;

            LoadSiteManagers();
            LoadCustomers();
        }

        #endregion

        #region Methods

        public void LoadProject(Project project)
        {
            Project = new ProjectWrapper(project);
            Project.Initialize(); // Ensure wrapper is synced with model
            
            // Sync selection dropdowns
            SiteManager = SiteManagers.FirstOrDefault(m => m.Id == project.SiteManagerId);
            Customer = Customers.FirstOrDefault(c => c.Id == project.CustomerId);

            Project.PropertyChanged += Project_PropertyChanged;
        }

        private async void Project_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectWrapper.StreetLine1))
            {
                // Only update suggestions if this wasn't a change from a selection
                await UpdateAddressSuggestions();
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
            if (Project == null) return;

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
            if (suggestion == null || Project == null) return;

            System.Diagnostics.Debug.WriteLine($"[EditProjectViewModel] Handling selection: {suggestion.Description}");

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
                System.Diagnostics.Debug.WriteLine("[EditProjectViewModel] Failed to get place details");
            }
        }

        private async void LoadSiteManagers()
        {
            var managers = await _staffRepository.GetAllAsync();
            SiteManagers.Clear();
            foreach (var m in managers) SiteManagers.Add(m);
            
            if (Project != null && SiteManager == null)
            {
                SiteManager = SiteManagers.FirstOrDefault(m => m.Id == Project.SiteManagerId);
            }
        }

        private async void LoadCustomers()
        {
            var customers = await _customerRepository.GetAllAsync();
            Customers.Clear();
            foreach (var c in customers) Customers.Add(c);

            if (Project != null && Customer == null)
            {
                Customer = Customers.FirstOrDefault(c => c.Id == Project.CustomerId);
            }
        }

        partial void OnSiteManagerChanged(Employee? value)
        {
            if (Project != null)
                Project.SiteManagerId = value?.Id;
        }

        partial void OnCustomerChanged(Customer? value)
        {
            if (Project != null)
            {
                Project.CustomerId = value?.Id;
                Project.Customer = value?.Name ?? string.Empty;
            }
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task SaveProject()
        {
            Project.Validate();
            if (Project.HasErrors)
            {
                IsErrorVisible = true;
                ValidationMessage = "Please fix the validation errors before saving.";
                return;
            }

            IsErrorVisible = false;
            ValidationMessage = string.Empty;

            try
            {
                IsBusy = true;
                Project.CommitToModel();
                await _projectRepository.UpdateAsync(Project.Model);
                
                _toastService.ShowSuccess("Project Updated", "The project has been saved successfully.");
                ProjectUpdated?.Invoke(this, EventArgs.Empty);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                IsErrorVisible = true;
                ValidationMessage = $"Failed to save project: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}




