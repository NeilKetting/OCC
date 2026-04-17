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
    public partial class NewItemViewModel : ViewModelBase
    {
        private readonly IInventoryService _inventoryService;

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

        public event Action<InventoryItem?>? Completed;

        public ObservableCollection<ItemType> ItemTypes { get; } = new(Enum.GetValues<ItemType>());

        public NewItemViewModel(string initialSku, IInventoryService inventoryService)
        {
            Sku = initialSku;
            _inventoryService = inventoryService;
            Title = "New Item";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                IsBusy = true;
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
            }
            catch (Exception)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    ErrorMessage = "Failed to save the new item. Please check the details.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            Completed?.Invoke(null);
        }
    }
}
