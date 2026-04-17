using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class InventoryViewModel : ViewModelBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly IToastService _toastService;
        private readonly ILogger<InventoryViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<InventoryItem> _items = new();

        [ObservableProperty]
        private string? _searchText;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _lowStockCount;

        private System.ComponentModel.ICollectionView? _itemsView;

        public InventoryViewModel(IInventoryService inventoryService, IToastService toastService, ILogger<InventoryViewModel> logger)
        {
            _inventoryService = inventoryService;
            _toastService = toastService;
            _logger = logger;
            Title = "Inventory Management";

            // Listen for stock updates
            WeakReferenceMessenger.Default.Register<StockUpdatedMessage>(this, (r, m) =>
            {
                var item = Items.FirstOrDefault(i => i.Id == m.Value.Id);
                if (item != null)
                {
                    _logger.LogInformation("Inventory item {ItemId} updated from message", m.Value.Id);
                    App.Current.Dispatcher.Invoke(async () => await LoadInventoryAsync());
                }
            });

            _logger.LogInformation("InventoryViewModel initialized");
            System.Windows.Application.Current.Dispatcher.InvokeAsync(LoadInventoryAsync);
        }

        [RelayCommand]
        private async Task LoadInventoryAsync()
        {
            try
            {
                _logger.LogInformation("Loading inventory items...");
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = true);
                
                var inventory = await _inventoryService.GetInventoryAsync();
                var list = inventory.ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Items.Clear();
                    foreach (var item in list) Items.Add(item);
                    
                    // Initialize View for filtering
                    _itemsView = System.Windows.Data.CollectionViewSource.GetDefaultView(Items);
                    _itemsView.Filter = FilterItems;

                    TotalCount = Items.Count;
                    LowStockCount = Items.Count(i => i.IsLowStock);
                });
                
                _logger.LogInformation("Successfully loaded {Count} inventory items", list.Count);
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

        partial void OnSearchTextChanged(string? value)
        {
            _itemsView?.Refresh();
        }

        private bool FilterItems(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            if (obj is InventoryItem item)
            {
                var search = SearchText.Trim();
                return item.Sku.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                       (item.Description != null && item.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            return true;
        }

        [RelayCommand]
        private void Search()
        {
            _itemsView?.Refresh();
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadInventoryAsync();
            _toastService.ShowInfo("Inventory", "Inventory refreshed.");
        }

        [RelayCommand]
        private void AddItem()
        {
            _logger.LogInformation("Add Item command triggered");
            _toastService.ShowInfo("Coming Soon", "The Add Item feature is currently under development.");
        }
    }
}
