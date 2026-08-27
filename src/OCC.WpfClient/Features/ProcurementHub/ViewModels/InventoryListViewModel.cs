using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class InventoryListViewModel : ListViewModelBase<InventoryItem>
    {
        private readonly IInventoryService _inventoryService;
        private readonly IToastService _toastService;
        private readonly ILogger<InventoryListViewModel> _logger;
        private readonly LocalSettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private List<InventoryItem> _allInventory = new();

        // Column Visibility
        [ObservableProperty] private bool _isSkuVisible = true;
        [ObservableProperty] private bool _isDescriptionVisible = true;
        [ObservableProperty] private bool _isCategoryVisible = true;
        [ObservableProperty] private bool _isQuantityVisible = true;
        [ObservableProperty] private bool _isJhbQuantityVisible = true;
        [ObservableProperty] private bool _isCptQuantityVisible = true;
        [ObservableProperty] private bool _isLocationVisible = true;
        [ObservableProperty] private bool _isStatusVisible = true;
        [ObservableProperty] private bool _isSupplierVisible = true;
        [ObservableProperty] private bool _isJhbReorderPointVisible = false;
        [ObservableProperty] private bool _isCptReorderPointVisible = false;
        [ObservableProperty] private bool _isUnitOfMeasureVisible = false;
        [ObservableProperty] private bool _isAverageCostVisible = false;
        [ObservableProperty] private bool _isPriceVisible = false;
        [ObservableProperty] private bool _isTrackLowStockVisible = false;
        [ObservableProperty] private bool _isTypeVisible = false;

        [ObservableProperty] private string _selectedCategoryFilter = "All Categories";
        [ObservableProperty] private string _selectedStatusFilter = "All Statuses";
        [ObservableProperty] private string _selectedBranchFilter = "All";

        [ObservableProperty] private int _lowStockCount;

        public override string ReportTitle => "Inventory Stock Report";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "SKU", PropertyName = "Sku", Width = 1.5 },
            new() { Header = "Description", PropertyName = "Description", Width = 4 },
            new() { Header = "Category", PropertyName = "Category", Width = 2 },
            new() { Header = "Location", PropertyName = "Location", Width = 1.5 },
            new() { Header = "Qty", PropertyName = "QuantityOnHand", Width = 1 }
        };

        // Standard commands for centralized UI
        public override IRelayCommand<object>? OpenCommand => OpenItemCommand;
        public override IRelayCommand<object>? EditCommand => EditItemCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteSelectedItemsCommand;

        public InventoryListViewModel(
            IInventoryService inventoryService, 
            IToastService toastService, 
            ILogger<InventoryListViewModel> logger,
            LocalSettingsService settingsService,
            IPdfService pdfService,
            IDialogService dialogService) : base(pdfService)
        {
            _inventoryService = inventoryService;
            _toastService = toastService;
            _logger = logger;
            _settingsService = settingsService;
            _dialogService = dialogService;
            Title = "Inventory Management";

            LoadLayout();

            // Listen for stock updates
            WeakReferenceMessenger.Default.Register<StockUpdatedMessage>(this, (r, m) =>
            {
                var item = Items.FirstOrDefault(i => i.Id == m.Value.Id);
                if (item != null)
                {
                    _logger.LogInformation("Inventory item {ItemId} updated from message", m.Value.Id);
                    App.Current.Dispatcher.Invoke(async () => await LoadDataAsync());
                }
            });

            _logger.LogInformation("InventoryListViewModel initialized");
            System.Windows.Application.Current.Dispatcher.InvokeAsync(LoadDataAsync);
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                _logger.LogInformation("Loading inventory items...");
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = true);
                
                var inventory = (await _inventoryService.GetInventoryAsync()).OrderBy(i => i.Sku).ToList();

                if (inventory.Count > 100)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _allInventory = inventory.Take(100).ToList();
                        FilterItems();
                        LowStockCount = inventory.Count(i => i.IsLowStock);
                        IsBusy = false; // Unblock UI
                    });

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            _allInventory = inventory;
                            FilterItems();
                            LowStockCount = _allInventory.Count(i => i.IsLowStock);
                        });
                    });
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _allInventory = inventory;
                        FilterItems();
                        LowStockCount = _allInventory.Count(i => i.IsLowStock);
                    });
                }
                
                _logger.LogInformation("Successfully loaded {Count} inventory items", inventory.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load inventory");
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    _toastService.ShowError("Error", $"Failed to load inventory: {ex.Message}"));
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = false);
            }
        }


        protected override void FilterItems()
        {
            var filtered = _allInventory.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                filtered = filtered.Where(i => SearchUtils.MatchesQuery(SearchQuery, i.Sku, i.Description, i.Category, i.UnitOfMeasure));
            }

            if (SelectedCategoryFilter != "All Categories" && !string.IsNullOrWhiteSpace(SelectedCategoryFilter))
            {
                var cat = SelectedCategoryFilter.ToLower();
                filtered = filtered.Where(i => i.Category?.ToLower() == cat);
            }

            if (SelectedStatusFilter != "All Statuses" && !string.IsNullOrWhiteSpace(SelectedStatusFilter))
            {
                if (SelectedStatusFilter == "OK")
                {
                    filtered = filtered.Where(i => i.Status == InventoryStatus.OK);
                }
                else if (SelectedStatusFilter == "Low Stock")
                {
                    filtered = filtered.Where(i => i.Status == InventoryStatus.Low);
                }
                else if (SelectedStatusFilter == "Out of Stock")
                {
                    filtered = filtered.Where(i => i.QuantityOnHand <= 0);
                }
            }

            if (SelectedBranchFilter != "All" && !string.IsNullOrWhiteSpace(SelectedBranchFilter))
            {
                if (SelectedBranchFilter == "JHB")
                {
                    filtered = filtered.Where(i => i.JhbQuantity > 0);
                }
                else if (SelectedBranchFilter == "CPT")
                {
                    filtered = filtered.Where(i => i.CptQuantity > 0);
                }
            }

            Items = new ObservableCollection<InventoryItem>(filtered.ToList());
            TotalCount = Items.Count;
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadDataAsync();
            _toastService.ShowInfo("Inventory", "Inventory refreshed.");
        }

        [RelayCommand]
        private void AddItem()
        {
            _logger.LogInformation("Add Item command triggered");
            var detailVm = new InventoryDetailViewModel(_inventoryService, _toastService, _logger);
            OpenOverlay(detailVm, async (res) =>
            {
                if (res is bool saved && saved)
                {
                    await LoadDataAsync();
                }
            });
        }

        [RelayCommand]
        private void OpenItem(object? parameter)
        {
            _ = EditItem(parameter);
        }

        [RelayCommand]
        private async Task EditItem(object? parameter)
        {
            var target = parameter as InventoryItem ?? SelectedItem;
            if (target == null) return;

            try
            {
                IsBusy = true;
                var item = await _inventoryService.GetInventoryItemAsync(target.Id);
                if (item != null)
                {
                    var detailVm = new InventoryDetailViewModel(_inventoryService, _toastService, _logger);
                    detailVm.SetItem(item);
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
                _logger.LogError(ex, "Error loading inventory item details");
                _toastService.ShowError("Error", "Could not load inventory item details. Please try again.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedItems(object? parameter)
        {
            var targets = GetDeleteTargets(parameter);
            if (!targets.Any()) return;

            string title = targets.Count > 1 ? "Delete Multiple Items" : "Delete Item";
            string message = targets.Count > 1
                ? $"You are about to delete {targets.Count} inventory items. This action cannot be undone. Are you sure you want to proceed?"
                : $"Are you sure you want to delete inventory item '{targets[0].Sku}'? This action cannot be undone.";

            var confirmed = await _dialogService.ShowConfirmationAsync(title, message);
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                foreach (var t in targets)
                {
                    await _inventoryService.DeleteItemAsync(t.Id);
                }
                await LoadDataAsync();
                _toastService.ShowSuccess("Success", targets.Count > 1 ? "Selected items deleted." : "Item deleted.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inventory items");
                _toastService.ShowError("Error", $"Failed to delete inventory item(s): {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public List<string> BranchOptions { get; } = new() { "All", "JHB", "CPT" };

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.InventoryListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsSkuVisible = layout.Columns.FirstOrDefault(c => c.Header == "SKU")?.IsVisible ?? true;
                IsDescriptionVisible = layout.Columns.FirstOrDefault(c => c.Header == "Description")?.IsVisible ?? true;
                IsCategoryVisible = layout.Columns.FirstOrDefault(c => c.Header == "Category")?.IsVisible ?? true;
                IsQuantityVisible = layout.Columns.FirstOrDefault(c => c.Header == "Quantity")?.IsVisible ?? true;
                IsJhbQuantityVisible = layout.Columns.FirstOrDefault(c => c.Header == "JHB Qty")?.IsVisible ?? true;
                IsCptQuantityVisible = layout.Columns.FirstOrDefault(c => c.Header == "CPT Qty")?.IsVisible ?? true;
                IsLocationVisible = layout.Columns.FirstOrDefault(c => c.Header == "Location")?.IsVisible ?? true;
                IsStatusVisible = layout.Columns.FirstOrDefault(c => c.Header == "Status")?.IsVisible ?? true;
                IsSupplierVisible = layout.Columns.FirstOrDefault(c => c.Header == "Supplier")?.IsVisible ?? true;
                IsJhbReorderPointVisible = layout.Columns.FirstOrDefault(c => c.Header == "JHB Reorder")?.IsVisible ?? false;
                IsCptReorderPointVisible = layout.Columns.FirstOrDefault(c => c.Header == "CPT Reorder")?.IsVisible ?? false;
                IsUnitOfMeasureVisible = layout.Columns.FirstOrDefault(c => c.Header == "UOM")?.IsVisible ?? false;
                IsAverageCostVisible = layout.Columns.FirstOrDefault(c => c.Header == "Avg Cost")?.IsVisible ?? false;
                IsPriceVisible = layout.Columns.FirstOrDefault(c => c.Header == "Price")?.IsVisible ?? false;
                IsTrackLowStockVisible = layout.Columns.FirstOrDefault(c => c.Header == "Track Low")?.IsVisible ?? false;
                IsTypeVisible = layout.Columns.FirstOrDefault(c => c.Header == "Type")?.IsVisible ?? false;
            }
            else
            {
                IsSkuVisible = true;
                IsDescriptionVisible = true;
                IsCategoryVisible = true;
                IsQuantityVisible = true;
                IsJhbQuantityVisible = true;
                IsCptQuantityVisible = true;
                IsLocationVisible = true;
                IsStatusVisible = true;
                IsSupplierVisible = true;
                IsJhbReorderPointVisible = false;
                IsCptReorderPointVisible = false;
                IsUnitOfMeasureVisible = false;
                IsAverageCostVisible = false;
                IsPriceVisible = false;
                IsTrackLowStockVisible = false;
                IsTypeVisible = false;
            }
        }

        private void SaveLayout()
        {
            var layout = new OCC.WpfClient.Infrastructure.Models.ListLayout
            {
                Columns = new System.Collections.Generic.List<OCC.WpfClient.Infrastructure.Models.ColumnConfig>
                {
                    new() { Header = "SKU", IsVisible = IsSkuVisible },
                    new() { Header = "Description", IsVisible = IsDescriptionVisible },
                    new() { Header = "Category", IsVisible = IsCategoryVisible },
                    new() { Header = "Quantity", IsVisible = IsQuantityVisible },
                    new() { Header = "JHB Qty", IsVisible = IsJhbQuantityVisible },
                    new() { Header = "CPT Qty", IsVisible = IsCptQuantityVisible },
                    new() { Header = "Location", IsVisible = IsLocationVisible },
                    new() { Header = "Status", IsVisible = IsStatusVisible },
                    new() { Header = "Supplier", IsVisible = IsSupplierVisible },
                    new() { Header = "JHB Reorder", IsVisible = IsJhbReorderPointVisible },
                    new() { Header = "CPT Reorder", IsVisible = IsCptReorderPointVisible },
                    new() { Header = "UOM", IsVisible = IsUnitOfMeasureVisible },
                    new() { Header = "Avg Cost", IsVisible = IsAverageCostVisible },
                    new() { Header = "Price", IsVisible = IsPriceVisible },
                    new() { Header = "Track Low", IsVisible = IsTrackLowStockVisible },
                    new() { Header = "Type", IsVisible = IsTypeVisible }
                }
            };
            _settingsService.Settings.InventoryListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsSkuVisibleChanged(bool value) => SaveLayout();
        partial void OnIsDescriptionVisibleChanged(bool value) => SaveLayout();
        partial void OnIsCategoryVisibleChanged(bool value) => SaveLayout();
        partial void OnIsQuantityVisibleChanged(bool value) => SaveLayout();
        partial void OnIsJhbQuantityVisibleChanged(bool value) => SaveLayout();
        partial void OnIsCptQuantityVisibleChanged(bool value) => SaveLayout();
        partial void OnIsLocationVisibleChanged(bool value) => SaveLayout();
        partial void OnIsStatusVisibleChanged(bool value) => SaveLayout();
        partial void OnIsSupplierVisibleChanged(bool value) => SaveLayout();
        partial void OnIsJhbReorderPointVisibleChanged(bool value) => SaveLayout();
        partial void OnIsCptReorderPointVisibleChanged(bool value) => SaveLayout();
        partial void OnIsUnitOfMeasureVisibleChanged(bool value) => SaveLayout();
        partial void OnIsAverageCostVisibleChanged(bool value) => SaveLayout();
        partial void OnIsPriceVisibleChanged(bool value) => SaveLayout();
        partial void OnIsTrackLowStockVisibleChanged(bool value) => SaveLayout();
        partial void OnIsTypeVisibleChanged(bool value) => SaveLayout();

        partial void OnSelectedCategoryFilterChanged(string value) => FilterItems();
        partial void OnSelectedStatusFilterChanged(string value) => FilterItems();
        partial void OnSelectedBranchFilterChanged(string value) => FilterItems();
    }
}
