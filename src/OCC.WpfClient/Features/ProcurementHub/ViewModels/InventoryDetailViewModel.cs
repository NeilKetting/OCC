using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class InventoryDetailViewModel : OverlayViewModel
    {
        private readonly IInventoryService _inventoryService;
        private readonly IToastService _toastService;
        private readonly ILogger _logger;

        [ObservableProperty] private InventoryItem _item = new();
        [ObservableProperty] private bool _isEditMode;

        public System.Collections.Generic.List<string> AvailableUOMs { get; } = new() { "ea", "m", "kg", "L", "m2", "m3", "box", "roll", "pack" };

        public InventoryDetailViewModel(
            IInventoryService inventoryService,
            IToastService toastService,
            ILogger logger)
        {
            _inventoryService = inventoryService;
            _toastService = toastService;
            _logger = logger;
            Title = "Add Stock Item";
        }

        public void SetItem(InventoryItem item)
        {
            Item = item;
            IsEditMode = true;
            Title = $"Edit {item.Sku}";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Item.Sku))
            {
                _toastService.ShowError("Validation", "SKU is required.");
                return;
            }

            try
            {
                IsBusy = true;
                Item.QuantityOnHand = Item.JhbQuantity + Item.CptQuantity;
                if (IsEditMode)
                    await _inventoryService.UpdateItemAsync(Item);
                else
                    await _inventoryService.CreateItemAsync(Item);

                _toastService.ShowSuccess("Success", "Inventory item saved.");
                Close(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving inventory item");
                _toastService.ShowError("Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
