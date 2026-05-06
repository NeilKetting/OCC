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

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class InventoryViewModel : ListViewModelBase<InventoryItem>
    {
        private readonly IInventoryService _inventoryService;
        private readonly IToastService _toastService;
        private readonly ILogger<InventoryViewModel> _logger;
        private readonly LocalSettingsService _settingsService;
        private List<InventoryItem> _allInventory = new();

        // Column Visibility
        [ObservableProperty] private bool _isSkuVisible = true;
        [ObservableProperty] private bool _isDescriptionVisible = true;
        [ObservableProperty] private bool _isCategoryVisible = true;
        [ObservableProperty] private bool _isQuantityVisible = true;
        [ObservableProperty] private bool _isLocationVisible = true;
        [ObservableProperty] private bool _isStatusVisible = true;

        [ObservableProperty] private bool _isColumnPickerOpen;

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

        private System.ComponentModel.ICollectionView? _itemsView;

        public InventoryViewModel(
            IInventoryService inventoryService, 
            IToastService toastService, 
            ILogger<InventoryViewModel> logger,
            LocalSettingsService settingsService,
            IPdfService pdfService) : base(pdfService)
        {
            _inventoryService = inventoryService;
            _toastService = toastService;
            _logger = logger;
            _settingsService = settingsService;
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

            _logger.LogInformation("InventoryViewModel initialized");
            System.Windows.Application.Current.Dispatcher.InvokeAsync(LoadDataAsync);
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                _logger.LogInformation("Loading inventory items...");
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = true);
                
                var inventory = await _inventoryService.GetInventoryAsync();
                _allInventory = inventory.OrderBy(i => i.Sku).ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    FilterItems();
                    LowStockCount = _allInventory.Count(i => i.IsLowStock);
                });
                
                _logger.LogInformation("Successfully loaded {Count} inventory items", _allInventory.Count);
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
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(i => 
                    (i.Sku?.ToLower().Contains(query) ?? false) ||
                    (i.Description?.ToLower().Contains(query) ?? false) ||
                    (i.Category?.ToLower().Contains(query) ?? false));
            }

            Items = new ObservableCollection<InventoryItem>(filtered.ToList());
            TotalCount = Items.Count;
        }


        [RelayCommand]
        private void Search()
        {
            _itemsView?.Refresh();
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
            _toastService.ShowInfo("Coming Soon", "The Add Item feature is currently under development.");
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.InventoryListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsSkuVisible = layout.Columns.FirstOrDefault(c => c.Header == "SKU")?.IsVisible ?? true;
                IsDescriptionVisible = layout.Columns.FirstOrDefault(c => c.Header == "Description")?.IsVisible ?? true;
                IsCategoryVisible = layout.Columns.FirstOrDefault(c => c.Header == "Category")?.IsVisible ?? true;
                IsQuantityVisible = layout.Columns.FirstOrDefault(c => c.Header == "Quantity")?.IsVisible ?? true;
                IsLocationVisible = layout.Columns.FirstOrDefault(c => c.Header == "Location")?.IsVisible ?? true;
                IsStatusVisible = layout.Columns.FirstOrDefault(c => c.Header == "Status")?.IsVisible ?? true;
            }
        }

        private void SaveLayout()
        {
            var layout = new Features.EmployeeHub.Models.EmployeeListLayout
            {
                Columns = new System.Collections.Generic.List<Features.EmployeeHub.Models.ColumnConfig>
                {
                    new() { Header = "SKU", IsVisible = IsSkuVisible },
                    new() { Header = "Description", IsVisible = IsDescriptionVisible },
                    new() { Header = "Category", IsVisible = IsCategoryVisible },
                    new() { Header = "Quantity", IsVisible = IsQuantityVisible },
                    new() { Header = "Location", IsVisible = IsLocationVisible },
                    new() { Header = "Status", IsVisible = IsStatusVisible }
                }
            };
            _settingsService.Settings.InventoryListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsSkuVisibleChanged(bool value) => SaveLayout();
        partial void OnIsDescriptionVisibleChanged(bool value) => SaveLayout();
        partial void OnIsCategoryVisibleChanged(bool value) => SaveLayout();
        partial void OnIsQuantityVisibleChanged(bool value) => SaveLayout();
        partial void OnIsLocationVisibleChanged(bool value) => SaveLayout();
        partial void OnIsStatusVisibleChanged(bool value) => SaveLayout();

        [RelayCommand]
        private void ToggleColumnPicker() => IsColumnPickerOpen = !IsColumnPickerOpen;
    }
}
