using System;
using System.Collections.Generic;
using System.Linq;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a commercial order within the OCC system (Purchase Order or Picking Order).
    /// Tracks the movement of materials between suppliers, inventory, and project sites.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>Orders</c> table.
    /// <b>How:</b> Orders contain multiple <see cref="OrderLine"/> items. They can be for procurement (<see cref="OrderType.PurchaseOrder"/>) 
    /// or for project-specific allocation (<see cref="OrderType.PickingOrder"/>).
    /// </remarks>
    public class Order : BaseEntity
    {

        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? ExpectedDeliveryDate { get; set; }
        public OrderType OrderType { get; set; } = OrderType.PurchaseOrder;
        public Branch Branch { get; set; } = Branch.JHB;
        
        // --- Entities Linkage ---
        
        // Purchase Order
        public Guid? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty; // Denormalized or Snapshot Name
        
        // Picking Order
        public Guid? CustomerId { get; set; }

        // --- Snapshot Data (Address/VAT/Phone at time of order) ---
        // This is generic to support both Supplier (PO) and Picking (PK)
        public string EntityAddress { get; set; } = string.Empty; 
        public string EntityTel { get; set; } = string.Empty;
        public string EntityVatNo { get; set; } = string.Empty;
        
        // --- Logistics ---
        public OrderDestinationType DestinationType { get; set; }
        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string Attention { get; set; } = string.Empty; // "Att: Jenna" linked to Project Contact

        // --- Financials ---
        public decimal TaxRate { get; set; } = 0.15m; // 15% VAT default
        public OrderStatus Status { get; set; } = OrderStatus.Ordered;
        public string Notes { get; set; } = string.Empty;
        public string DeliveryInstructions { get; set; } = string.Empty;
        public string ScopeOfWork { get; set; } = string.Empty;
        public string Template { get; set; } = "Standard PO";
        public string Terms { get; set; } = "Net 30";
        public string ReferenceNo { get; set; } = string.Empty; // P.O. NO. in QuickBooks

        // --- Content ---
        public System.Collections.ObjectModel.ObservableCollection<OrderLine> Lines { get; set; } = new();

        // --- Validation / Display Helpers ---
        public string DestinationDisplay => DestinationType == OrderDestinationType.Site 
            ? $"Site: {ProjectName}" 
            : "Office Stock";

        public int TotalItems => Lines?.Count ?? 0;

        // Calculated Totals
        public decimal SubTotal => Lines?.Sum(l => l.LineTotal) ?? 0;
        public decimal VatTotal => Lines?.Sum(l => l.VatAmount) ?? 0;
        private decimal? _totalAmount;
        public decimal TotalAmount 
        { 
            get => _totalAmount ?? (SubTotal + VatTotal);
            set => _totalAmount = value;
        }
    }

    public enum OrderType
    {
        PurchaseOrder,     // Buying from Supplier
        PickingOrder,      // Issuing to Site / Project
        ReturnToInventory  // Returning leftover material from Project to Stock
    }

    public enum OrderDestinationType
    {
        Stock,
        Site
    }

    public enum OrderStatus
    {
        Draft,
        Ordered,
        PartialDelivery,
        Completed,
        Finalised,
        Cancelled
    }
}
