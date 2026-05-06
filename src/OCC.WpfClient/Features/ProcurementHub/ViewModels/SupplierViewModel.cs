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

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class SupplierViewModel : ListViewModelBase<SupplierSummaryDto>
    {
        private readonly ISupplierService _supplierService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<SupplierViewModel> _logger;
        private readonly LocalSettingsService _settingsService;
        private List<SupplierSummaryDto> _allSuppliers = new();

        public override string ReportTitle => "Supplier Directory";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Supplier Name", PropertyName = "Name", Width = 3 },
            new() { Header = "Contact", PropertyName = "ContactPerson", Width = 2 },
            new() { Header = "Phone", PropertyName = "Phone", Width = 1.5 },
            new() { Header = "VAT #", PropertyName = "VatNumber", Width = 1.5 }
        };

        // Column Visibility
        [ObservableProperty] private bool _isBranchVisible = true;
        [ObservableProperty] private bool _isContactVisible = true;
        [ObservableProperty] private bool _isPhoneVisible = true;
        
        [ObservableProperty] private bool _isColumnPickerOpen;

        [ObservableProperty] private string _selectedBranchFilter = "All";

        public List<string> BranchOptions { get; } = new List<string> { "All" }.Concat(Enum.GetNames(typeof(Branch))).ToList();

        public SupplierViewModel(
            ISupplierService supplierService,
            IDialogService dialogService,
            LocalSettingsService settingsService,
            ILogger<SupplierViewModel> logger,
            IPdfService pdfService) : base(pdfService)
        {
            _supplierService = supplierService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _logger = logger;
            Title = "Supplier Management";

            LoadLayout();
            _ = LoadDataAsync();
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.SupplierListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsBranchVisible = layout.Columns.FirstOrDefault(c => c.Header == "Branch")?.IsVisible ?? true;
                IsContactVisible = layout.Columns.FirstOrDefault(c => c.Header == "Contact")?.IsVisible ?? true;
                IsPhoneVisible = layout.Columns.FirstOrDefault(c => c.Header == "Phone")?.IsVisible ?? true;
            }
        }

        private void SaveLayout()
        {
            var layout = new Features.EmployeeHub.Models.EmployeeListLayout
            {
                Columns = new List<Features.EmployeeHub.Models.ColumnConfig>
                {
                    new() { Header = "Branch", IsVisible = IsBranchVisible },
                    new() { Header = "Contact", IsVisible = IsContactVisible },
                    new() { Header = "Phone", IsVisible = IsPhoneVisible }
                }
            };
            _settingsService.Settings.SupplierListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsBranchVisibleChanged(bool value) => SaveLayout();
        partial void OnIsContactVisibleChanged(bool value) => SaveLayout();
        partial void OnIsPhoneVisibleChanged(bool value) => SaveLayout();

        [RelayCommand]
        private void ToggleColumnPicker() => IsColumnPickerOpen = !IsColumnPickerOpen;

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading suppliers...";

                var suppliers = await _supplierService.GetSupplierSummariesAsync();
                _allSuppliers = suppliers.OrderBy(s => s.Name).ToList();

                FilterItems();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading suppliers");
                await _dialogService.ShowAlertAsync("Error", $"Failed to load suppliers: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddSupplier()
        {
            var supplier = new Supplier();
            OpenOverlay(new SupplierDetailViewModel(this, supplier, _supplierService, _dialogService, _logger, _pdfService));
        }

        [RelayCommand]
        private async Task EditSupplier(SupplierSummaryDto? summary)
        {
            var target = summary ?? SelectedItem;
            if (target == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Loading details...";
                var supplier = await _supplierService.GetSupplierAsync(target.Id);
                if (supplier != null)
                {
                    OpenOverlay(new SupplierDetailViewModel(this, supplier, _supplierService, _dialogService, _logger, _pdfService));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading supplier details");
                await _dialogService.ShowAlertAsync("Error", "Could not load supplier details. Please try again.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteSupplier(SupplierSummaryDto? summary)
        {
            var target = summary ?? SelectedItem;
            if (target == null) return;

            var confirmed = await _dialogService.ShowConfirmationAsync("Delete Supplier",
                $"Are you sure you want to delete '{target.Name}'? This action cannot be undone.");

            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Deleting supplier...";
                await _supplierService.DeleteSupplierAsync(target.Id);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting supplier");
                await _dialogService.ShowAlertAsync("Error", $"Failed to delete supplier: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedBranchFilterChanged(string value) => FilterItems();

        protected override void FilterItems()
        {
            IEnumerable<SupplierSummaryDto> filtered = _allSuppliers;

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(s =>
                    (s.Name?.ToLower().Contains(query) ?? false) ||
                    (s.Email?.ToLower().Contains(query) ?? false) ||
                    (s.Phone?.ToLower().Contains(query) ?? false) ||
                    (s.VatNumber?.ToLower().Contains(query) ?? false));
            }

            if (SelectedBranchFilter != "All" && Enum.TryParse<Branch>(SelectedBranchFilter, out var branch))
            {
                var branchStr = branch.ToString();
                filtered = filtered.Where(s => s.Branch == null || s.Branch == branchStr);
            }

            var result = filtered.ToList();
            Items = new ObservableCollection<SupplierSummaryDto>(result);
            TotalCount = result.Count;
        }

        public void CloseDetailView() => CloseOverlay();
    }
}
