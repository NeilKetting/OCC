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

namespace OCC.WpfClient.Features.CustomerHub.ViewModels
{
    public partial class CustomerListViewModel : OverlayHostViewModel
    {
        private readonly ICustomerService _customerService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<CustomerListViewModel> _logger;
        private List<CustomerSummaryDto> _allCustomers = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<CustomerSummaryDto> _customers = new();

        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private CustomerSummaryDto? _selectedCustomer;

        public CustomerListViewModel(
            ICustomerService customerService,
            IDialogService dialogService,
            ILogger<CustomerListViewModel> logger)
        {
            _customerService = customerService;
            _dialogService = dialogService;
            _logger = logger;
            Title = "Customer Management";
            
            _ = LoadData();
        }

        [RelayCommand]
        public async Task LoadData()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading customers...";
                
                var customers = await _customerService.GetCustomerSummariesAsync();
                _allCustomers = customers.OrderBy(c => c.Name).ToList();
                
                FilterCustomers();
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

        [RelayCommand]
        private void AddCustomer()
        {
            var customer = new Customer();
            OpenOverlay(new CustomerDetailViewModel(this, customer, _customerService, _dialogService, _logger));
        }

        [RelayCommand]
        private async Task EditCustomer(CustomerSummaryDto? summary)
        {
            if (summary == null) return;
            
            try
            {
                IsBusy = true;
                BusyText = "Loading details...";
                var customer = await _customerService.GetCustomerAsync(summary.Id);
                if (customer != null)
                {
                    OpenOverlay(new CustomerDetailViewModel(this, customer, _customerService, _dialogService, _logger));
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteCustomer(CustomerSummaryDto? summary)
        {
            if (summary == null) return;
            
            var confirmed = await _dialogService.ShowConfirmationAsync("Delete Customer", 
                $"Are you sure you want to delete '{summary.Name}'? This action cannot be undone.");

            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Deleting customer...";
                var success = await _customerService.DeleteCustomerAsync(summary.Id);
                if (success)
                {
                    await LoadData();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSearchQueryChanged(string value) => FilterCustomers();

        private void FilterCustomers()
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
            Customers = new ObservableCollection<CustomerSummaryDto>(result);
            TotalCount = _allCustomers.Count;
        }

        public void CloseDetailView()
        {
            CloseOverlay();
        }
    }
}
