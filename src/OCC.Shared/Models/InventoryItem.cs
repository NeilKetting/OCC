using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a stock item or material in the warehouse inventory.
    /// Used for tracking stock levels, reorder points, and costs across different branches.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>InventoryItems</c> table.
    /// <b>How:</b> Integrated with the Ordering system. When <see cref="QuantityOnHand"/> falls below <see cref="ReorderPoint"/>, 
    /// the item is flagged as "Low Stock" in the dashboard.
    /// </remarks>
    public enum ItemType
    {
        Service,
        StockPart,
        NonStockPart,
        OtherCharge
    }

    public class InventoryItem : BaseEntity
    {

        
        /// <summary>
        /// Detailed description of the product (e.g., "Cement 50kg").
        /// This property was previously named <c>ProductName</c>.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The primary supplier for this item (stored as string).
        /// </summary>
        public string Supplier { get; set; } = string.Empty;

        /// <summary>
        /// Category of the inventory item (e.g., "General", "Building").
        /// </summary>
        public string Category { get; set; } = "General";

        /// <summary>
        /// Physical location in the warehouse.
        /// </summary>
        public string Location { get; set; } = "Warehouse"; // Rack A, Shelf 1        
        /// <summary> Stock level specifically at the Johannesburg branch. </summary>
        public double JhbQuantity { get; set; }

        /// <summary> Stock level specifically at the Cape Town branch. </summary>
        public double CptQuantity { get; set; }

        /// <summary> Total aggregate quantity across all branches. </summary>
        public double QuantityOnHand { get; set; }

        /// <summary> The stock level threshold at which a restock should be triggered for JHB. </summary>
        public double JhbReorderPoint { get; set; }

        /// <summary> The stock level threshold at which a restock should be triggered for CPT. </summary>
        public double CptReorderPoint { get; set; }

        /// <summary> The unit in which the item is measured (e.g., "ea", "kg", "m"). </summary>
        public string UnitOfMeasure { get; set; } = "ea";
        
        /// <summary> Stock Keeping Unit - unique alphanumeric ID for the product type. </summary>
        public string Sku { get; set; } = string.Empty;

        /// <summary> The calculated average cost of purchasing this item over time. </summary>
        public decimal AverageCost { get; set; }

        /// <summary> The standard internal or retail price. </summary>
        public decimal Price { get; set; }

        /// <summary> If true, system provides alerts when stock falls below reorder point. </summary>
        public bool TrackLowStock { get; set; } = true;

        /// <summary> The type of item (Service, Stock Part, etc.) </summary>
        public ItemType Type { get; set; } = ItemType.StockPart;

        // Status
        public InventoryStatus Status => TrackLowStock && (JhbQuantity <= JhbReorderPoint || CptQuantity <= CptReorderPoint) ? InventoryStatus.Low : InventoryStatus.OK;
        
        // Alias for View Binding compatibility
        public InventoryStatus InventoryStatus => Status;

        public bool IsLowStock => Status == InventoryStatus.Low;
    }

    public enum InventoryStatus
    {
        OK,
        Low,
        Critical
    }
}
