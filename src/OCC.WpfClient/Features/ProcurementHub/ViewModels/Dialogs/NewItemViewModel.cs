using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using System.Collections.ObjectModel;
using System;
using OCC.WpfClient.Services.Interfaces;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs
{
    public partial class NewItemViewModel : OverlayViewModel
    {
        private readonly IInventoryService _inventoryService;
        private System.Collections.Generic.List<InventoryItem> _existingItems = new();

        [ObservableProperty]
        private ItemType _type = ItemType.StockPart;

        [ObservableProperty]
        private string _sku = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private decimal _rate;

        [ObservableProperty]
        private string _vatCode = "S";

        [ObservableProperty]
        private string _account = "Sales";

        [ObservableProperty]
        private bool _isSubitem;

        [ObservableProperty]
        private string? _parentItem;

        [ObservableProperty]
        private bool _isInactive;

        public event Action<InventoryItem?>? Completed;

        public ObservableCollection<ItemType> ItemTypes { get; } = new(Enum.GetValues<ItemType>());
        public ObservableCollection<string> ParentItems { get; } = new();

        public NewItemViewModel(string initialSku, IInventoryService inventoryService)
        {
            Sku = initialSku;
            _inventoryService = inventoryService;
            Title = "New Item";
            _ = LoadExistingItemsAsync();
        }

        private async Task LoadExistingItemsAsync()
        {
            try
            {
                var items = await _inventoryService.GetInventoryAsync();
                if (items != null)
                {
                    _existingItems = new System.Collections.Generic.List<InventoryItem>(items);
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ParentItems.Clear();
                        foreach (var item in _existingItems)
                        {
                            ParentItems.Add(item.Sku);
                        }
                    });
                }
            }
            catch
            {
                // Safe ignore or log
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Sku))
            {
                ErrorMessage = "SKU/Item Code is required.";
                return;
            }

            // Enforce duplicate SKU validation
            if (_existingItems.Exists(i => string.Equals(i.Sku, Sku, StringComparison.OrdinalIgnoreCase)))
            {
                ErrorMessage = $"An item with SKU/Item Code '{Sku}' already exists.";
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = null;
                var newItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    Sku = Sku,
                    Description = Description,
                    Price = Rate,
                    Type = Type,
                    UnitOfMeasure = "ea" // Default for now
                };

                // Save immediately as requested
                var createdItem = await _inventoryService.CreateItemAsync(newItem);
                Completed?.Invoke(createdItem);
                Close(createdItem);
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    ErrorMessage = ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private new void Cancel()
        {
            Completed?.Invoke(null);
            Close();
        }
    }
}
