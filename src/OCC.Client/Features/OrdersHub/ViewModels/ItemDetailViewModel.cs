using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging; // NEW

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    /// <summary>
    /// ViewModel for adding or editing individual inventory items.
    /// Manages item properties such as SKU, category, supplier, and stock levels.
    /// </summary>

    public partial class ItemDetailViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly Services.Infrastructure.OrderStateService _orderStateService;
        private Guid _editingId;

        #endregion

        #region Observables



        /// <summary>
        /// Gets or sets the Stock Keeping Unit (SKU) for the item.
        /// </summary>
        [ObservableProperty]
        private string _sku = string.Empty;

        /// <summary>
        /// Gets or sets the description of the product.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        private string _description = string.Empty;

        public new string Title
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Description))
                {
                    return IsEditMode ? "Edit Item" : "Add New Item";
                }
                return Description;
            }
        }

        /// <summary>
        /// Gets or sets the category the item belongs to.
        /// </summary>
        [ObservableProperty]
        private string _category = "General";
        
        /// <summary>
        /// Gets or sets the name of the supplier associated with this item.
        /// </summary>
        [ObservableProperty]
        private string _supplierName = string.Empty;

        /// <summary>
        /// Gets or sets the physical storage location of the item.
        /// </summary>
        [ObservableProperty]
        private string _location = "Warehouse";

        /// <summary>
        /// Gets or sets the unit of measure (e.g., "ea", "kg", "m").
        /// </summary>
        [ObservableProperty]
        private string _unitOfMeasure = "ea";

        /// <summary>
        /// Gets the total quantity of items in stock (JHB + CPT).
        /// </summary>
        public double QuantityOnHand => JhbQuantity + CptQuantity;

        /// <summary>
        /// Gets or sets the stock level at which a reorder should be triggered.
        /// </summary>
        [ObservableProperty]
        private double _reorderPoint;
        
        /// <summary>
        /// Gets or sets the standard unit price of the item.
        /// </summary>
        [ObservableProperty]
        private decimal _price;

        /// <summary>
        /// Gets or sets the average cost per unit of the item.
        /// </summary>
        [ObservableProperty]
        private decimal _averageCost;

        /// <summary>
        /// Gets or sets a value indicating whether to track low stock for this item.
        /// </summary>
        [ObservableProperty]
        private bool _isTrackingLowStock = true;
        
        /// <summary>
        /// Gets or sets a value indicating whether stock levels are tracked for this item.
        /// Compatibility wrapper for the Type property.
        /// </summary>
        public bool IsStockItem
        {
            get => Type == ItemType.StockPart;
            set
            {
                if (value)
                    Type = ItemType.StockPart;
                else if (Type == ItemType.StockPart)
                    Type = ItemType.Service;
                
                OnPropertyChanged(nameof(IsStockItem));
            }
        }

        /// <summary>
        /// Gets or sets the type of inventory item (Service, Stock Part, etc.).
        /// </summary>
        [ObservableProperty]
        private ItemType _type = ItemType.StockPart;
        
        /// <summary>
        /// Gets or sets the quantity in JHB branch.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(QuantityOnHand))]
        private double _jhbQuantity;

        /// <summary>
        /// Gets or sets the quantity in CPT branch.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(QuantityOnHand))]
        private double _cptQuantity;

        /// <summary>
        /// Gets or sets the stock level at which a restock should be triggered for JHB.
        /// </summary>
        [ObservableProperty]
        private double _jhbReorderPoint;

        /// <summary>
        /// Gets or sets the stock level at which a restock should be triggered for CPT.
        /// </summary>
        [ObservableProperty]
        private double _cptReorderPoint;

        /// <summary>
        /// Gets or sets the concurrency token.
        /// </summary>
        [ObservableProperty]
        private byte[]? _rowVersion;

        /// <summary>
        /// Gets or sets a value indicating whether an asynchronous operation is in progress.
        /// </summary>


        /// <summary>
        /// Gets or sets a value indicating whether the view is in edit mode.
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// Gets the collection of available product categories for selection.
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<string> AvailableCategories { get; } = new();

        /// <summary>
        /// Gets the collection of available suppliers for assignment.
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<Supplier> AvailableSuppliers { get; } = new();

        /// <summary>
        /// Gets the list of standard units of measure.
        /// </summary>
        public System.Collections.Generic.List<string> AvailableUOMs { get; } = new() { "ea", "m", "kg", "L", "m2", "m3", "box", "roll", "pack" };

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemDetailViewModel"/> class with required dependencies.
        /// </summary>
        /// <param name="orderManager">Central manager for inventory and supplier operations.</param>
        /// <param name="dialogService">Service for user notifications.</param>
        /// <param name="orderStateService">Service for maintaining order state during navigation.</param>
        public ItemDetailViewModel(
            IOrderManager orderManager, 
            IDialogService dialogService, 
            Services.Infrastructure.OrderStateService orderStateService)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _orderStateService = orderStateService;
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to save the inventory item details via the Order Manager.
        /// Validates input and triggers navigation callback if necessary.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [RelayCommand]
        public virtual async Task Save()
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                await _dialogService.ShowAlertAsync("Validation", "Description is required to save the item.");
                return;
            }

            try
            {
                BusyText = "Saving inventory item...";
                IsBusy = true;

                InventoryItem item = new InventoryItem
                {
                    Id = IsEditMode ? _editingId : Guid.NewGuid(),
                    Sku = Sku,
                    Description = Description,
                    Category = Category,
                    Supplier = SupplierName ?? string.Empty,
                    Location = Location,
                    UnitOfMeasure = UnitOfMeasure,
                    JhbQuantity = JhbQuantity,
                    CptQuantity = CptQuantity,
                    QuantityOnHand = QuantityOnHand, // Will be computed or ignored by API largely, but good to send
                    JhbReorderPoint = JhbReorderPoint,
                    CptReorderPoint = CptReorderPoint,
                    AverageCost = AverageCost,
                    Price = Price,
                    TrackLowStock = IsTrackingLowStock,
                    Type = Type,
                    RowVersion = RowVersion ?? Array.Empty<byte>()
                };

                if (IsEditMode)
                {
                    await _orderManager.UpdateItemAsync(item);
                }
                else
                {
                    await _orderManager.CreateItemAsync(item);
                }
                
                ItemSaved?.Invoke(this, EventArgs.Empty);
                CloseRequested?.Invoke(this, EventArgs.Empty);
                
                if (_orderStateService.HasSavedState)
                {
                     WeakReferenceMessenger.Default.Send(new OCC.Client.Messages.NavigationRequestMessage("CreateOrder"));
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"An error occurred while saving: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Command to cancel the current operation and close the view without saving.
        /// </summary>
        [RelayCommand]
        public void Cancel()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the ViewModel for adding or editing an inventory item.
        /// </summary>
        /// <param name="item">The item to edit, or null to create a new item.</param>
        /// <param name="categories">Optional list of existing categories to populate the dropdown.</param>
        public virtual void Load(InventoryItem? item, System.Collections.Generic.List<string>? categories = null)
        {
            AvailableCategories.Clear();
            if (categories != null)
            {
                foreach (var c in categories) AvailableCategories.Add(c);
            }
            
            _ = LoadSuppliersAsync();

            if (item == null)
            {
                IsEditMode = false;
                Sku = "";
                Description = "";
                Category = "General";
                SupplierName = "";
                Location = "Warehouse";
                UnitOfMeasure = "ea";
                JhbQuantity = 0;
                CptQuantity = 0;
                JhbReorderPoint = 10;
                CptReorderPoint = 10;
                Price = 0;
                Type = ItemType.StockPart; 
                RowVersion = null;
            }
            else
            {
                IsEditMode = true;
                _editingId = item.Id;
                Sku = item.Sku;
                Description = item.Description;
                Category = item.Category;
                SupplierName = item.Supplier;
                Location = item.Location;
                UnitOfMeasure = item.UnitOfMeasure;
                JhbQuantity = item.JhbQuantity;
                CptQuantity = item.CptQuantity;
                // QuantityOnHand is computed
                AverageCost = item.AverageCost;
                Price = item.Price;
                IsTrackingLowStock = item.TrackLowStock;
                Type = item.Type;
                JhbReorderPoint = item.JhbReorderPoint;
                CptReorderPoint = item.CptReorderPoint;
                RowVersion = item.RowVersion;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Event raised when the user requests to close the detail view.
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// Event raised after an item has been successfully saved.
        /// </summary>
        public event EventHandler? ItemSaved;

        /// <summary>
        /// Asynchronously retrieves all registered suppliers to populate the selection dropdown.
        /// </summary>
        private async Task LoadSuppliersAsync()
        {
             try
             {
                 var suppliers = await _orderManager.GetSuppliersAsync();
                 AvailableSuppliers.Clear();
                 foreach (var s in suppliers.OrderBy(x => x.Name))
                 {
                     AvailableSuppliers.Add(s);
                 }
             }
             catch (Exception ex)
             {
                 System.Diagnostics.Debug.WriteLine($"Error loading suppliers for item detail: {ex.Message}");
             }
        }

        #endregion
    }
}
