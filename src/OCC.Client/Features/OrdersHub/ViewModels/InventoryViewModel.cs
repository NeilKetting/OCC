using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.Client.ViewModels.Core;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    /// <summary>
    /// ViewModel for managing and viewing inventory stock levels.
    /// Provides filtering and detail view navigation for inventory items.
    /// </summary>
    /// <summary>
    /// ViewModel for managing and viewing inventory stock levels.
    /// Provides filtering and detail view navigation for inventory items.
    /// </summary>
    public partial class InventoryViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly IInventoryImportService _importService;
        private readonly ILogger<InventoryViewModel> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuthService _authService;
        private List<InventorySummaryDto> _allItems = new();

        #endregion

        #region Observables

        /// <summary>
        /// Gets the collection of inventory items matching the current filter.
        /// </summary>
        public ObservableCollection<InventorySummaryDto> InventoryItems { get; } = new();

        /// <summary>
        /// Gets or sets the search query to filter inventory items by name.
        /// </summary>
        [ObservableProperty]
        private string _searchQuery = "";

        /// <summary>
        /// Gets or sets the currently selected branch filter.
        /// </summary>
        [ObservableProperty]
        private Branch? _selectedBranch;

        /// <summary>
        /// Gets the list of available branches for filtering.
        /// </summary>
        public List<Branch> AvailableBranches { get; } = Enum.GetValues<Branch>().ToList();



        /// <summary>
        /// Gets or sets a value indicating whether the item details popup is visible.
        /// </summary>
        [ObservableProperty]
        private bool _isDetailVisible;

        /// <summary>
        /// Gets or sets the inventory item currently selected for viewing or editing.
        /// </summary>
        [ObservableProperty]
        private InventorySummaryDto? _selectedInventoryItem;

        /// <summary>
        /// Gets or sets the ViewModel for the inventory item detail view.
        /// </summary>
        [ObservableProperty]
        private ItemDetailViewModel? _detailViewModel;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="InventoryViewModel"/> class with required dependencies.
        /// </summary>
        /// <param name="orderManager">Manager providing centralized order and inventory operations.</param>
        /// <param name="dialogService">Service for displaying user notifications.</param>
        /// <param name="logger">Logger for capturing diagnostic information.</param>
        /// <param name="serviceProvider">Service provider for resolving child ViewModels via DI.</param>
        /// <param name="authService">Service for accessing current user context.</param>
        public InventoryViewModel(
            IOrderManager orderManager, 
            IDialogService dialogService, 
            IInventoryImportService importService,
            ILogger<InventoryViewModel> logger,
            IServiceProvider serviceProvider,
            IAuthService authService)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _importService = importService;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _authService = authService;
            
            // Default to user's branch if available, otherwise JHB
            SelectedBranch = _authService.CurrentUser?.Branch ?? Branch.JHB;

            _ = LoadInventoryAsync();
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to manually refresh the inventory list.
        /// </summary>
        [RelayCommand]
        public virtual async Task ImportInventory()
        {
            try
            {
                var filePath = await _dialogService.PickFileAsync("Select Inventory List CSV", new[] { "*.csv" });

                if (!string.IsNullOrEmpty(filePath))
                {
                    IsBusy = true;
                    BusyText = "Importing inventory...";
                    
                    await using var stream = System.IO.File.OpenRead(filePath);
                    var (items, failed, errors) = await _importService.ImportInventoryAsync(stream);
                    var importedCount = 0;

                    foreach (var item in items)
                    {
                        if (string.IsNullOrWhiteSpace(item.Sku))
                        {
                            var newSku = await _dialogService.ShowInputAsync(
                                "Missing SKU", 
                                $"Item '{item.Description}' is missing a SKU.\n\nPlease enter a SKU to import this item, or Cancel to skip it.", 
                                item.Description);

                            if (string.IsNullOrWhiteSpace(newSku))
                            {
                                errors.Add($"Skipped '{item.Description}': No SKU provided.");
                                failed++;
                                continue;
                            }
                            item.Sku = newSku;
                        }

                        // Double check name if it was empty (unlikely with our mapping)
                        if (string.IsNullOrWhiteSpace(item.Description))
                        {
                             item.Description = "Unknown Product"; 
                        }

                        try
                        {
                            await _orderManager.CreateItemAsync(item);
                            importedCount++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Failed to save '{item.Description}': {ex.Message}");
                            failed++;
                        }
                    }

                    if (failed == 0)
                    {
                        await _dialogService.ShowAlertAsync("Success", $"Successfully imported {importedCount} items.");
                    }
                    else
                    {
                        var errorMsg = $"Imported: {importedCount}\nSkipped/Failed: {failed}\n\nErrors:\n" + string.Join("\n", errors.Take(10));
                        if (errors.Count > 10) errorMsg += "\n...";
                        
                        await _dialogService.ShowAlertAsync("Import Result", errorMsg);
                    }
                    
                    await LoadInventoryAsync();
                }
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error importing inventory");
                 await _dialogService.ShowAlertAsync("Error", $"Import failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Command to open the detail view for a specific inventory item in edit mode.
        /// </summary>
        /// <param name="item">The inventory item to edit.</param>
        [RelayCommand]
        public virtual async Task EditInventoryItem(InventorySummaryDto summary)
        {
            if (summary == null) return;
            
            BusyText = "Loading item details...";
            IsBusy = true;
            var item = await _orderManager.GetItemByIdAsync(summary.Id);
            IsBusy = false;

            if (item == null)
            {
                await _dialogService.ShowAlertAsync("Error", "Could not load inventory item details.");
                return;
            }

            var categories = _allItems.Select(i => i.Category).Distinct().OrderBy(c => c).ToList();

            DetailViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ItemDetailViewModel>(_serviceProvider);
            DetailViewModel.Load(item, categories); 
            
            DetailViewModel.CloseRequested += (s, e) => IsDetailVisible = false;
            DetailViewModel.ItemSaved += (s, e) => 
            {
                IsDetailVisible = false;
                _ = LoadInventoryAsync();
            };
            
            IsDetailVisible = true;
        }

        /// <summary>
        /// Command to open the detail view for creating a new inventory item.
        /// </summary>
        [RelayCommand]
        public virtual void AddInventoryItem()
        {
            var categories = _allItems.Select(i => i.Category).Distinct().OrderBy(c => c).ToList();

            DetailViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ItemDetailViewModel>(_serviceProvider);
            DetailViewModel.Load(null, categories);

            DetailViewModel.CloseRequested += (s, e) => IsDetailVisible = false;
            DetailViewModel.ItemSaved += (s, e) =>
            {
                IsDetailVisible = false;
                _ = LoadInventoryAsync();
            };

            IsDetailVisible = true;
        }

        /// <summary>
        /// Command to permanently delete an inventory item.
        /// </summary>
        /// <param name="item">The item to be deleted.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [RelayCommand]
        public virtual async Task DeleteInventoryItem(InventorySummaryDto summary)
        {
            if (summary == null) return;

            var confirm = await _dialogService.ShowConfirmationAsync("Confirm Delete", 
                $"Are you sure you want to delete '{summary.Description}'?\n\nThis action cannot be undone.");

            if (!confirm) return;

            try
            {
                IsBusy = true;
                BusyText = "Deleting item...";
                await _orderManager.DeleteItemAsync(summary.Id);
                await LoadInventoryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inventory item {ItemId}", summary.Id);
                
                // Check if it's a conflict error (item in use)
                if (ex.Message.Contains("Conflict", StringComparison.OrdinalIgnoreCase) || 
                    ex.Message.Contains("used in existing", StringComparison.OrdinalIgnoreCase))
                {
                    await _dialogService.ShowAlertAsync("Cannot Delete", 
                        "This item cannot be deleted because it has historical usage (orders, etc.).\n\nYou should rename it or mark it as 'Inactive' if supported.");
                }
                else
                {
                    await _dialogService.ShowAlertAsync("Error", $"Failed to delete item: {ex.Message}");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Asynchronously loads all inventory items from the Order Manager and applies the current search filter.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task LoadInventoryAsync()
        {
            try
            {
                BusyText = "Loading inventory...";
                IsBusy = true;
                _allItems = (await _orderManager.GetInventorySummariesAsync()).ToList();
                FilterItems();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading inventory list");
                if(_dialogService != null) await _dialogService.ShowAlertAsync("Error", $"Failed to retrieve inventory items: {ex.Message}");
            }
             finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Filters the full collection of items based on the current search query.
        /// </summary>
        private void FilterItems()
        {
            InventoryItems.Clear();
            var filtered = string.IsNullOrWhiteSpace(SearchQuery) 
                ? _allItems 
                : _allItems.Where(i => i.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

            foreach (var item in filtered)
            {
                InventoryItems.Add(item);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Responds to changes in the search query by reapplying the filter to the inventory list.
        /// </summary>
        partial void OnSearchQueryChanged(string value) => FilterItems();

        #endregion
    }
}
