using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    public partial class InventoryLookupViewModel : ViewModelBase
    {
        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly ILogger<InventoryLookupViewModel> _logger;

        private IEnumerable<InventoryItem> _allInventoryMaster = Enumerable.Empty<InventoryItem>();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private InventoryItem? _selectedItem;

        public ObservableCollection<InventoryItem> FilteredItems { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        public InventoryLookupViewModel() 
        {
            _orderManager = null!;
            _dialogService = null!;
            _logger = null!;

            // Design-time data
            Categories.Add("General");
            Categories.Add("Materials");
            FilteredItems.Add(new InventoryItem { Sku = "ITEM-001", Description = "Sample Item 1", Price = 50 });
            FilteredItems.Add(new InventoryItem { Sku = "ITEM-002", Description = "Sample Item 2", Price = 150 });
        }

        public InventoryLookupViewModel(
            IOrderManager orderManager,
            IDialogService dialogService,
            ILogger<InventoryLookupViewModel> logger)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _logger = logger;
        }

        public virtual void Initialize(IEnumerable<InventoryItem> inventory)
        {
            _allInventoryMaster = inventory;
            
            var cats = inventory.Select(x => x.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c);
            Categories.Clear();
            foreach(var c in cats) Categories.Add(c);
            if (!Categories.Contains("General")) Categories.Add("General");

            Filter();
        }

        [RelayCommand]
        public void Filter()
        {
            // Guard: If we are already filtering or if SearchText matches selected item, skip to avoid UI loops/crashes
            if (SelectedItem != null && SearchText == SelectedItem.Description) return;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredItems.Clear();
                foreach (var item in _allInventoryMaster) FilteredItems.Add(item);
                return;
            }

            var search = SearchText.Trim();
            var filtered = _allInventoryMaster
                .Where(i => (i.Description != null && i.Description.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                            (i.Sku != null && i.Sku.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(i => (i.Sku ?? "").ToLower()).Select(g => g.First())
                .OrderByDescending(i => i.Sku != null && i.Sku.Equals(search, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(i => i.Description != null && i.Description.Equals(search, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(i => i.Sku != null && i.Sku.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(i => i.Description != null && i.Description.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                .ThenBy(i => i.Description)
                .ToList();

            FilteredItems.Clear();
            foreach (var item in filtered) FilteredItems.Add(item);
        }

        partial void OnSearchTextChanged(string value)
        {
            // Use Post to decouple filtering from the property change notification
            // This prevents ArgumentOutOfRangeException in Avalonia's selecting items control during collection reset
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Filter(), Avalonia.Threading.DispatcherPriority.Background);
        }
    }
}
