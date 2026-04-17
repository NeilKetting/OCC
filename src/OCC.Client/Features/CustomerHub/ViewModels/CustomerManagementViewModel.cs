using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.CustomerHub.ViewModels
{
    public partial class CustomerManagementViewModel : ViewModelBase, IRecipient<OCC.Client.ViewModels.Messages.EntityUpdatedMessage>
    {
        #region Private Members

        private readonly Services.Interfaces.ICustomerService _customerService;
        private readonly Services.Interfaces.IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private List<CustomerSummaryDto> _allCustomers = new();

        #endregion

        #region Observables

        [ObservableProperty]
        private string _activeTab = "Customers";

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<CustomerSummaryDto> _customers = new();

        [ObservableProperty]
        private bool _isAddCustomerPopupVisible;

        [ObservableProperty]
        private CustomerDetailViewModel? _customerDetailPopup;

        [ObservableProperty]
        private CustomerSummaryDto? _selectedCustomer;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _busyText = string.Empty;

        #endregion

        #region Constructors

        public CustomerManagementViewModel()
        {
            // Designer constructor
            _customerService = null!; // Corrected from _customerRepository
            _dialogService = null!;
            _serviceProvider = null!;
        }

        public CustomerManagementViewModel(
            Services.Interfaces.ICustomerService customerService,
            Services.Interfaces.IDialogService dialogService,
            IServiceProvider serviceProvider)
        {
            _customerService = customerService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            
            _ = LoadData();

            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        public void Receive(OCC.Client.ViewModels.Messages.EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "Customer")
            {
                _ = LoadData();
            }
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task AddCustomer()
        {
            await OpenDetailPopup(null);
        }

        [RelayCommand]
        public async Task EditCustomer(CustomerSummaryDto customer)
        {
            if (customer == null) return;
            await OpenDetailPopup(customer);
        }

        [RelayCommand]
        public async Task DeleteCustomer(CustomerSummaryDto customer)
        {
            if (customer == null) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Customer", 
                $"Are you sure you want to delete '{customer.Name}'? This may affect linked projects.");

            if (confirmed)
            {
                try
                {
                    BusyText = $"Deleting {customer.Name}...";
                    IsBusy = true;
                    await _customerService.DeleteCustomerAsync(customer.Id);
                    await LoadData();
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowAlertAsync("Error", $"Failed to delete customer: {ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private void CloseDetail()
        {
            IsAddCustomerPopupVisible = false;
            CustomerDetailPopup = null;
        }

        #endregion

        #region Methods

        private async Task OpenDetailPopup(CustomerSummaryDto? summary)
        {
            var vm = new CustomerDetailViewModel(_customerService, _dialogService);
            if (summary != null)
            {
                BusyText = "Loading details...";
                IsBusy = true;
                var full = await _customerService.GetCustomerAsync(summary.Id);
                IsBusy = false;
                
                if (full != null) vm.Load(full);
                else { await _dialogService.ShowAlertAsync("Error", "Could not load customer details."); return; }
            }
            else
            {
                vm.InitializeNew();
            }

            vm.CloseRequested += (s, e) => CloseDetail();
            vm.Saved += (s, e) => 
            {
                CloseDetail();
                _ = LoadData();
            };

            CustomerDetailPopup = vm;
            IsAddCustomerPopupVisible = true;
        }
        
        // Overload for Add (no params)
        private void OpenDetailPopup() => _ = OpenDetailPopup(null);

        public async Task LoadData()
        {
            try
            {
                BusyText = "Loading customers...";
                IsBusy = true;

                var customers = await _customerService.GetCustomerSummariesAsync();
                _allCustomers = customers.OrderBy(c => c.Name).ToList();

                FilterCustomers();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading customers: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSearchQueryChanged(string value)
        {
            FilterCustomers();
        }

        private void FilterCustomers()
        {
            if (_allCustomers == null) return;

            var filtered = _allCustomers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(c => 
                    (c.Name?.ToLower().Contains(query) ?? false) ||
                    (c.Email?.ToLower().Contains(query) ?? false) ||
                    (c.Address?.ToLower().Contains(query) ?? false)
                );
            }

            var resultList = filtered.ToList();
            Customers = new ObservableCollection<CustomerSummaryDto>(resultList);
            TotalCount = resultList.Count;
        }

        #endregion
    }
}
