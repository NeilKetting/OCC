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
        private readonly ICustomerService _customerService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<CustomerListViewModel> _logger;
        private readonly LocalSettingsService _settingsService;
        private List<CustomerSummaryDto> _allCustomers = new();

        public override string ReportTitle => "Customer Directory";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Name", PropertyName = "Name", Width = 2 },
            new() { Header = "Email", PropertyName = "Email", Width = 2 },
            new() { Header = "Phone", PropertyName = "Phone", Width = 1.5 },
            new() { Header = "Contact Person", PropertyName = "ContactPerson", Width = 1.5 }
        };

        [ObservableProperty] private bool _isEmailVisible = true;
        [ObservableProperty] private bool _isPhoneVisible = true;
        
        [ObservableProperty] private bool _isColumnPickerOpen;

        public CustomerListViewModel(
            ICustomerService customerService,
            IDialogService dialogService,
            LocalSettingsService settingsService,
            ILogger<CustomerListViewModel> logger,
            IPdfService pdfService) : base(pdfService)
        {
            _customerService = customerService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _logger = logger;
            Title = "Customer Management";
            
            LoadLayout();
            _ = LoadDataAsync();
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.CustomerListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsEmailVisible = layout.Columns.FirstOrDefault(c => c.Header == "Email")?.IsVisible ?? true;
                IsPhoneVisible = layout.Columns.FirstOrDefault(c => c.Header == "Phone")?.IsVisible ?? true;
            }
        }

        private void SaveLayout()
        {
            var layout = new Features.EmployeeHub.Models.EmployeeListLayout
            {
                Columns = new List<Features.EmployeeHub.Models.ColumnConfig>
                {
                    new() { Header = "Email", IsVisible = IsEmailVisible },
                    new() { Header = "Phone", IsVisible = IsPhoneVisible }
                }
            };
            _settingsService.Settings.CustomerListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsEmailVisibleChanged(bool value) => SaveLayout();
        partial void OnIsPhoneVisibleChanged(bool value) => SaveLayout();

        [RelayCommand]
        private void ToggleColumnPicker() => IsColumnPickerOpen = !IsColumnPickerOpen;

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading customers...";
                
                var customers = await _customerService.GetCustomerSummariesAsync();
                _allCustomers = customers.OrderBy(c => c.Name).ToList();
                
                FilterItems();
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
            OpenOverlay(new CustomerDetailViewModel(this, customer, _customerService, _dialogService, _logger, _pdfService));
        }

        [RelayCommand]
        private async Task EditCustomer(CustomerSummaryDto? summary)
        {
            var target = summary ?? SelectedItem;
            if (target == null) return;
            
            try
            {
                IsBusy = true;
                BusyText = "Loading details...";
                var customer = await _customerService.GetCustomerAsync(target.Id);
                if (customer != null)
                {
                    OpenOverlay(new CustomerDetailViewModel(this, customer, _customerService, _dialogService, _logger, _pdfService));
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
            var target = summary ?? SelectedItem;
            if (target == null) return;
            
            var confirmed = await _dialogService.ShowConfirmationAsync("Delete Customer", 
                $"Are you sure you want to delete '{target.Name}'? This action cannot be undone.");

            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Deleting customer...";
                var success = await _customerService.DeleteCustomerAsync(target.Id);
                if (success)
                {
                    await LoadDataAsync();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

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

        public void CloseDetailView() => CloseOverlay();
    }
}
