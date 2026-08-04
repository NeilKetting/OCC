using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a single line item within an <see cref="Order"/>.
    /// Contains details about the product, quantity, pricing, and fulfillment status.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>OrderLines</c> table.
    /// <b>How:</b> Totals are calculated via <see cref="CalculateTotal"/>. This model is
    /// intentionally free of INotifyPropertyChanged — UI change notification is the
    /// responsibility of <c>OrderLineWrapper</c> in the WPF client layer.
    /// </remarks>
    public class OrderLine : BaseEntity
    {
        /// <summary>Foreign Key linking to the parent <see cref="Order"/>.</summary>
        public Guid OrderId { get; set; }

        /// <summary>Link to the specific <see cref="InventoryItem"/>. Required for all orders.</summary>
        public Guid? InventoryItemId { get; set; }

        /// <summary>SKU or code identifying the item (copied from InventoryItem or entered manually).</summary>
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>Detailed description of the product or service.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Grouping category (e.g., "Materials", "Labour").</summary>
        public string Category { get; set; } = "General";

        /// <summary>Quantity of units requested.</summary>
        public double QuantityOrdered { get; set; }

        /// <summary>Quantity of units already delivered or fulfilled.</summary>
        public double QuantityReceived { get; set; }

        /// <summary>Unit of measurement (e.g., "kg", "m", "hours").</summary>
        public string UnitOfMeasure { get; set; } = string.Empty;

        /// <summary>Price per single unit (excluding VAT).</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>Calculated VAT amount for this line.</summary>
        public decimal VatAmount { get; set; }

        /// <summary>Total cost for this line (Quantity * Unit Price) excluding VAT.</summary>
        public decimal LineTotal { get; set; }

        /// <summary>Optional free-text remarks for this line.</summary>
        public string Remarks { get; set; } = string.Empty;

        // ─── EF Core Navigation ───────────────────────────────────────────────────

        /// <summary>
        /// EF Core reverse-navigation to the parent <see cref="OCC.Shared.Models.Order"/>.
        /// Used by the API for queries that navigate from lines to their parent order
        /// (e.g., filtering by order type or date). Not used by the WPF client — the
        /// client always works with the parent <see cref="Order"/> object directly.
        /// </summary>
        public Order? Order { get; set; }

        /// <summary>Calculated remaining items to be fulfilled.</summary>
        public double RemainingQuantity => Math.Max(0, QuantityOrdered - QuantityReceived);

        /// <summary>True if the order line has been fully fulfilled.</summary>
        public bool IsComplete => QuantityReceived >= QuantityOrdered;

        /// <summary>
        /// Recalculates <see cref="LineTotal"/> and <see cref="VatAmount"/> based on
        /// current quantity and unit price.
        /// </summary>
        /// <param name="taxRate">The applicable tax rate (e.g., 0.15 for 15%).</param>
        public void CalculateTotal(decimal taxRate)
        {
            decimal qty = (decimal)QuantityOrdered;
            decimal sub = qty * UnitPrice;
            LineTotal = Math.Round(sub, 2, MidpointRounding.AwayFromZero);
            VatAmount = Math.Round(LineTotal * taxRate, 2, MidpointRounding.AwayFromZero);
        }
    }
}
