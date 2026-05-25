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
        private readonly ConnectionSettings _connectionSettings;
        private List<CustomerSummaryDto> _allCustomers = new();

        public override string ReportTitle => "Customer Directory";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Name", PropertyName = "Name", Width = 2 },
            new() { Header = "Email", PropertyName = "Email", Width = 2 },
            new() { Header = "Phone", PropertyName = "Phone", Width = 1.5 },
            new() { Header = "Contact Person", PropertyName = "ContactPerson", Width = 1.5 }
        };

        [ObservableProperty] private bool _isNameVisible = true;
        [ObservableProperty] private bool _isEmailVisible = true;
        [ObservableProperty] private bool _isPhoneVisible = true;
        [ObservableProperty] private bool _isActionsVisible = true;
        
        

        // Standard commands for centralized UI
        public override IRelayCommand<object>? OpenCommand => OpenCustomerCommand;
        public override IRelayCommand<object>? EditCommand => EditCustomerCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteSelectedCustomersCommand;

        public CustomerListViewModel(
            ICustomerService customerService,
            IDialogService dialogService,
            LocalSettingsService settingsService,
            ILogger<CustomerListViewModel> logger,
            IPdfService pdfService,
            ConnectionSettings connectionSettings) : base(pdfService)
        {
            _customerService = customerService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _logger = logger;
            _connectionSettings = connectionSettings;
            Title = "Customer Management";
            
            LoadLayout();
            _ = LoadDataAsync();
        }

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

        private void SaveLayout()
        {
            var layout = new Features.EmployeeHub.Models.EmployeeListLayout
            {
                Columns = new List<Features.EmployeeHub.Models.ColumnConfig>
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
            var detailVm = new CustomerDetailViewModel(customer, _customerService, _dialogService, _logger, _pdfService, _connectionSettings);
            OpenOverlay(detailVm, async (res) =>
            {
                if (res is bool saved && saved)
                {
                    await LoadDataAsync();
                }
            });
        }

        [RelayCommand]
        private void OpenCustomer(object? parameter)
        {
            _ = EditCustomer(parameter);
        }

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

        
    }
}
