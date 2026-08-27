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
    public partial class SupplierListViewModel : ListViewModelBase<SupplierSummaryDto>
    {
        private readonly ISupplierService _supplierService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<SupplierListViewModel> _logger;
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
        [ObservableProperty] private bool _isEmailVisible = true;
        [ObservableProperty] private bool _isAddressVisible = false;
        [ObservableProperty] private bool _isCityVisible = false;
        [ObservableProperty] private bool _isVatNumberVisible = false;
        [ObservableProperty] private bool _isBankNameVisible = false;
        [ObservableProperty] private bool _isBankAccountNumberVisible = false;
        [ObservableProperty] private bool _isBranchCodeVisible = false;
        [ObservableProperty] private bool _isSupplierAccountNumberVisible = false;
        
        

        [ObservableProperty] private string _selectedBranchFilter = "All";

        public List<string> BranchOptions { get; } = new List<string> { "All" }.Concat(Enum.GetNames(typeof(Branch))).ToList();

        // Standard commands for centralized UI
        public override IRelayCommand<object>? OpenCommand => OpenSupplierCommand;
        public override IRelayCommand<object>? EditCommand => EditSupplierCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteSelectedSuppliersCommand;

        private readonly ISignalRService? _signalRService;

        public SupplierListViewModel(
            ISupplierService supplierService,
            IDialogService dialogService,
            LocalSettingsService settingsService,
            ILogger<SupplierListViewModel> logger,
            IPdfService pdfService,
            ISignalRService? signalRService = null) : base(pdfService)
        {
            _supplierService = supplierService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _logger = logger;
            _signalRService = signalRService;
            Title = "Supplier Management";

            if (_signalRService != null)
            {
                _signalRService.OnSupplierChanged += OnSupplierChangedReceived;
            }

            LoadLayout();
            _ = LoadDataAsync();
        }

        private void OnSupplierChangedReceived(EntityChangeDto<SupplierSummaryDto> change)
        {
            if (change?.Entity == null) return;
            App.Current?.Dispatcher.Invoke(() =>
            {
                var existing = _allSuppliers.FirstOrDefault(s => s.Id == change.EntityId || s.Id == change.Entity.Id);
                if (change.Action == "Created" || change.Action == "Create")
                {
                    if (existing == null) _allSuppliers.Add(change.Entity);
                    else _allSuppliers[_allSuppliers.IndexOf(existing)] = change.Entity;
                }
                else if (change.Action == "Updated" || change.Action == "Update")
                {
                    if (existing != null) _allSuppliers[_allSuppliers.IndexOf(existing)] = change.Entity;
                    else _allSuppliers.Add(change.Entity);
                }
                else if (change.Action == "Deleted" || change.Action == "Delete")
                {
                    if (existing != null) _allSuppliers.Remove(existing);
                }
                FilterItems();
            });
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.SupplierListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsBranchVisible = layout.Columns.FirstOrDefault(c => c.Header == "Branch")?.IsVisible ?? true;
                IsContactVisible = layout.Columns.FirstOrDefault(c => c.Header == "Contact")?.IsVisible ?? true;
                IsPhoneVisible = layout.Columns.FirstOrDefault(c => c.Header == "Phone")?.IsVisible ?? true;
                IsEmailVisible = layout.Columns.FirstOrDefault(c => c.Header == "Email")?.IsVisible ?? true;
                IsAddressVisible = layout.Columns.FirstOrDefault(c => c.Header == "Address")?.IsVisible ?? false;
                IsCityVisible = layout.Columns.FirstOrDefault(c => c.Header == "City")?.IsVisible ?? false;
                IsVatNumberVisible = layout.Columns.FirstOrDefault(c => c.Header == "VAT #")?.IsVisible ?? false;
                IsBankNameVisible = layout.Columns.FirstOrDefault(c => c.Header == "Bank Name")?.IsVisible ?? false;
                IsBankAccountNumberVisible = layout.Columns.FirstOrDefault(c => c.Header == "Bank Acc #")?.IsVisible ?? false;
                IsBranchCodeVisible = layout.Columns.FirstOrDefault(c => c.Header == "Branch Code")?.IsVisible ?? false;
                IsSupplierAccountNumberVisible = layout.Columns.FirstOrDefault(c => c.Header == "Supplier Acc #")?.IsVisible ?? false;
            }
            else
            {
                IsBranchVisible = true;
                IsContactVisible = true;
                IsPhoneVisible = true;
                IsEmailVisible = true;
                IsAddressVisible = false;
                IsCityVisible = false;
                IsVatNumberVisible = false;
                IsBankNameVisible = false;
                IsBankAccountNumberVisible = false;
                IsBranchCodeVisible = false;
                IsSupplierAccountNumberVisible = false;
            }
        }

        private void SaveLayout()
        {
            var layout = new OCC.WpfClient.Infrastructure.Models.ListLayout
            {
                Columns = new List<OCC.WpfClient.Infrastructure.Models.ColumnConfig>
                {
                    new() { Header = "Branch", IsVisible = IsBranchVisible },
                    new() { Header = "Contact", IsVisible = IsContactVisible },
                    new() { Header = "Phone", IsVisible = IsPhoneVisible },
                    new() { Header = "Email", IsVisible = IsEmailVisible },
                    new() { Header = "Address", IsVisible = IsAddressVisible },
                    new() { Header = "City", IsVisible = IsCityVisible },
                    new() { Header = "VAT #", IsVisible = IsVatNumberVisible },
                    new() { Header = "Bank Name", IsVisible = IsBankNameVisible },
                    new() { Header = "Bank Acc #", IsVisible = IsBankAccountNumberVisible },
                    new() { Header = "Branch Code", IsVisible = IsBranchCodeVisible },
                    new() { Header = "Supplier Acc #", IsVisible = IsSupplierAccountNumberVisible }
                }
            };
            _settingsService.Settings.SupplierListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsBranchVisibleChanged(bool value) => SaveLayout();
        partial void OnIsContactVisibleChanged(bool value) => SaveLayout();
        partial void OnIsPhoneVisibleChanged(bool value) => SaveLayout();
        partial void OnIsEmailVisibleChanged(bool value) => SaveLayout();
        partial void OnIsAddressVisibleChanged(bool value) => SaveLayout();
        partial void OnIsCityVisibleChanged(bool value) => SaveLayout();
        partial void OnIsVatNumberVisibleChanged(bool value) => SaveLayout();
        partial void OnIsBankNameVisibleChanged(bool value) => SaveLayout();
        partial void OnIsBankAccountNumberVisibleChanged(bool value) => SaveLayout();
        partial void OnIsBranchCodeVisibleChanged(bool value) => SaveLayout();
        partial void OnIsSupplierAccountNumberVisibleChanged(bool value) => SaveLayout();

        

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading suppliers...";

                var suppliers = (await _supplierService.GetSupplierSummariesAsync()).OrderBy(s => s.Name).ToList();

                if (suppliers.Count > 100)
                {
                    // Step 1: Fast render top 100 records
                    _allSuppliers = suppliers.Take(100).ToList();
                    FilterItems();
                    IsBusy = false; // Unblock UI

                    // Step 2: Background hydration of full dataset
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        App.Current?.Dispatcher.Invoke(() =>
                        {
                            _allSuppliers = suppliers;
                            FilterItems();
                        });
                    });
                }
                else
                {
                    _allSuppliers = suppliers;
                    FilterItems();
                }
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
            var detailVm = new SupplierDetailViewModel(supplier, _supplierService, _dialogService, _logger, _pdfService);
            OpenOverlay(detailVm, async (res) =>
            {
                if (res is bool saved && saved)
                {
                    await LoadDataAsync();
                }
            });
        }

        [RelayCommand]
        private void OpenSupplier(object? parameter)
        {
            _ = EditSupplier(parameter);
        }

        [RelayCommand]
        private async Task EditSupplier(object? parameter)
        {
            var target = parameter as SupplierSummaryDto ?? SelectedItem;
            if (target == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Loading details...";
                var supplier = await _supplierService.GetSupplierAsync(target.Id);
                if (supplier != null)
                {
                    var detailVm = new SupplierDetailViewModel(supplier, _supplierService, _dialogService, _logger, _pdfService);
                    OpenOverlay(detailVm, async (res) =>
                    {
                        if (res is bool saved && saved)
                        {
                            await LoadDataAsync();
                        }
                    });
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
        private async Task DeleteSelectedSuppliers(object? parameter)
        {
            var targets = GetDeleteTargets(parameter);
            if (!targets.Any()) return;

            string title = targets.Count > 1 ? "Delete Multiple Suppliers" : "Delete Supplier";
            string message = targets.Count > 1 
                ? $"You are about to delete {targets.Count} records. This action cannot be undone. Are you sure you want to proceed?"
                : $"Are you sure you want to delete supplier '{targets[0].Name}'? This action cannot be undone.";

            var confirmed = await _dialogService.ShowConfirmationAsync(title, message);
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = targets.Count > 1 ? "Deleting suppliers..." : "Deleting supplier...";
                foreach (var t in targets)
                {
                    await _supplierService.DeleteSupplierAsync(t.Id);
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

        partial void OnSelectedBranchFilterChanged(string value) => FilterItems();

        protected override void FilterItems()
        {
            var filtered = _allSuppliers.AsEnumerable();

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

        
    }
}
