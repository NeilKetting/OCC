using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Features.CustomerHub.ViewModels
{
    public partial class CustomerListViewModel : ListViewModelBase<CustomerSummaryDto>
    {
        #region Private Fields

        // Service for managing customer database operations and API calls
        private readonly ICustomerService _customerService;

        // Displays alerts and confirmation dialogs
        private readonly IDialogService _dialogService;

        // Logger for customer list telemetry and exception tracking
        private readonly ILogger<CustomerListViewModel> _logger;

        // Manages local workspace preferences, including list layouts
        private readonly LocalSettingsService _settingsService;

        // Holds connection settings, like API base URLs
        private readonly ConnectionSettings _connectionSettings;

        // Backing collection of all customers retrieved from the service
        private List<CustomerSummaryDto> _allCustomers = new();

        #endregion

        #region Properties & Observables

        // Title of the customer report when printed
        public override string ReportTitle => "Customer Directory";

        // Definition of the columns in the printed customer report
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Name", PropertyName = "Name", Width = 2 },
            new() { Header = "Email", PropertyName = "Email", Width = 2 },
            new() { Header = "Phone", PropertyName = "Phone", Width = 1.5 },
            new() { Header = "Contact Person", PropertyName = "ContactPerson", Width = 1.5 }
        };

        // Controls visibility of the customer Name column in the list view
        [ObservableProperty]
        private bool _isNameVisible = true;

        // Controls visibility of the customer Email column in the list view
        [ObservableProperty]
        private bool _isEmailVisible = true;

        // Controls visibility of the customer Phone column in the list view
        [ObservableProperty]
        private bool _isPhoneVisible = true;

        // Controls visibility of actions (Edit, Delete) in the list view
        [ObservableProperty]
        private bool _isActionsVisible = true;

        // Centralized UI commands mapped to the shell action bar
        public override IRelayCommand<object>? OpenCommand => OpenCustomerCommand;
        public override IRelayCommand<object>? EditCommand => EditCustomerCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteSelectedCustomersCommand;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the customer list view model, loads layout preferences, and starts background data load.
        /// </summary>
        private readonly ISignalRService? _signalRService;

        public CustomerListViewModel(
            ICustomerService customerService,
            IDialogService dialogService,
            LocalSettingsService settingsService,
            ILogger<CustomerListViewModel> logger,
            IPdfService pdfService,
            ConnectionSettings connectionSettings,
            ISignalRService? signalRService = null) : base(pdfService)
        {
            _customerService = customerService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _logger = logger;
            _connectionSettings = connectionSettings;
            _signalRService = signalRService;
            Title = "Customer Management";
            
            if (_signalRService != null)
            {
                _signalRService.OnCustomerChanged += OnCustomerChangedReceived;
            }

            LoadLayout();
            _ = LoadDataAsync();
        }

        private void OnCustomerChangedReceived(EntityChangeDto<CustomerSummaryDto> change)
        {
            if (change?.Entity == null) return;
            App.Current?.Dispatcher.Invoke(() =>
            {
                var existing = _allCustomers.FirstOrDefault(c => c.Id == change.EntityId || c.Id == change.Entity.Id);
                if (change.Action == "Created" || change.Action == "Create")
                {
                    if (existing == null) _allCustomers.Add(change.Entity);
                    else _allCustomers[_allCustomers.IndexOf(existing)] = change.Entity;
                }
                else if (change.Action == "Updated" || change.Action == "Update")
                {
                    if (existing != null) _allCustomers[_allCustomers.IndexOf(existing)] = change.Entity;
                    else _allCustomers.Add(change.Entity);
                }
                else if (change.Action == "Deleted" || change.Action == "Delete")
                {
                    if (existing != null) _allCustomers.Remove(existing);
                }
                FilterItems();
            });
        }

        #endregion

        #region Commands

        /// <summary>
        /// Opens the overlay to add a new customer record.
        /// </summary>
        [RelayCommand]
        private void AddCustomer()
        {
            var customer = new Customer();
            var detailVm = new CustomerDetailViewModel(customer, _customerService, _dialogService, _logger, _pdfService, _connectionSettings);
            OpenOverlay(detailVm, async (res) =>
            {
                if (res is bool saved && saved)
                {
                    await LoadDataAsync();
                }
            });
        }

        /// <summary>
        /// Command alias for editing the customer record.
        /// </summary>
        [RelayCommand]
        private void OpenCustomer(object? parameter)
        {
            _ = EditCustomer(parameter);
        }

        /// <summary>
        /// Resolves customer details and opens the details overlay for editing.
        /// </summary>
        [RelayCommand]
        private async Task EditCustomer(object? parameter)
        {
            var target = parameter as CustomerSummaryDto ?? SelectedItem;
            if (target == null) return;
            
            try
            {
                IsBusy = true;
                BusyText = "Loading details...";
                var customer = await _customerService.GetCustomerAsync(target.Id);
                if (customer != null)
                {
                    var detailVm = new CustomerDetailViewModel(customer, _customerService, _dialogService, _logger, _pdfService, _connectionSettings);
                    OpenOverlay(detailVm, async (res) =>
                    {
                        if (res is bool saved && saved)
                        {
                            await LoadDataAsync();
                        }
                    });
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Deletes the selected customer or bulk list of selected customers after prompting for confirmation.
        /// </summary>
        [RelayCommand]
        private async Task DeleteSelectedCustomers(object? parameter)
        {
            List<CustomerSummaryDto> targets = new();
            if (parameter is System.Collections.IList list)
            {
                targets = list.Cast<CustomerSummaryDto>().ToList();
            }
            else if (parameter is CustomerSummaryDto summary)
            {
                targets.Add(summary);
            }
            else if (SelectedItem != null)
            {
                targets.Add(SelectedItem);
            }

            if (!targets.Any()) return;

            string message = targets.Count > 1 
                ? $"Are you sure you want to delete {targets.Count} selected customers? This action cannot be undone."
                : $"Are you sure you want to delete '{targets[0].Name}'? This action cannot be undone.";

            var confirmed = await _dialogService.ShowConfirmationAsync("Delete Customer", message);
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Deleting...";
                foreach (var t in targets)
                {
                    await _customerService.DeleteCustomerAsync(t.Id);
                }
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk delete failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Asynchronously fetches customer summary records from the database using progressive staged loading.
        /// </summary>
        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading customers...";
                
                var customers = (await _customerService.GetCustomerSummariesAsync()).OrderBy(c => c.Name).ToList();

                if (customers.Count > 100)
                {
                    // Step 1: Render top 100 rows instantly so user can start working immediately
                    _allCustomers = customers.Take(100).ToList();
                    FilterItems();
                    IsBusy = false; // Unblock UI

                    // Step 2: Hydrate full dataset seamlessly in background
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        App.Current?.Dispatcher.Invoke(() =>
                        {
                            _allCustomers = customers;
                            FilterItems();
                        });
                    });
                }
                else
                {
                    _allCustomers = customers;
                    FilterItems();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customers");
            }
            finally
            {
                IsBusy = false;
            }
        }


        /// <summary>
        /// Filters the cached list of customers based on the user's active search query.
        /// </summary>
        protected override void FilterItems()
        {
            var filtered = _allCustomers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(c => 
                    (c.Name?.ToLower().Contains(query) ?? false) ||
                    (c.Email?.ToLower().Contains(query) ?? false) ||
                    (c.Address?.ToLower().Contains(query) ?? false));
            }

            var result = filtered.ToList();
            Items = new ObservableCollection<CustomerSummaryDto>(result);
            TotalCount = result.Count;
        }

        /// <summary>
        /// Reads custom column layout configuration from settings and applies it to column visibility toggles.
        /// </summary>
        private void LoadLayout()
        {
            var layout = _settingsService.Settings.CustomerListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsNameVisible = layout.Columns.FirstOrDefault(c => c.Header == "Name")?.IsVisible ?? true;
                IsEmailVisible = layout.Columns.FirstOrDefault(c => c.Header == "Email")?.IsVisible ?? true;
                IsPhoneVisible = layout.Columns.FirstOrDefault(c => c.Header == "Phone")?.IsVisible ?? true;
                IsActionsVisible = layout.Columns.FirstOrDefault(c => c.Header == "Actions")?.IsVisible ?? true;
            }
        }

        /// <summary>
        /// Builds layout model representing visible columns and saves it to local settings.
        /// </summary>
        private void SaveLayout()
        {
            var layout = new OCC.WpfClient.Infrastructure.Models.ListLayout
            {
                Columns = new List<OCC.WpfClient.Infrastructure.Models.ColumnConfig>
                {
                    new() { Header = "Name", IsVisible = IsNameVisible },
                    new() { Header = "Email", IsVisible = IsEmailVisible },
                    new() { Header = "Phone", IsVisible = IsPhoneVisible },
                    new() { Header = "Actions", IsVisible = IsActionsVisible }
                }
            };
            _settingsService.Settings.CustomerListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsNameVisibleChanged(bool value) => SaveLayout();
        partial void OnIsEmailVisibleChanged(bool value) => SaveLayout();
        partial void OnIsPhoneVisibleChanged(bool value) => SaveLayout();
        partial void OnIsActionsVisibleChanged(bool value) => SaveLayout();

        #endregion
    }
}
