using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a single line item within an <see cref="Order"/>.
    /// Contains details about the product, quantity, pricing, and fulfillment status.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>OrderLines</c> table.
    /// <b>How:</b> Manages its own change interactions to update UI totals dynamically.
    /// </remarks>
    public class OrderLine : BaseEntity, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private string _itemCode = string.Empty;
        private string _description = string.Empty;
        private double _quantityOrdered;
        private string _unitOfMeasure = "";
        private decimal _unitPrice;
        private decimal _lineTotal;
        private string _remarks = string.Empty;


        /// <summary> Foreign Key linking to the parent <see cref="Order"/>. </summary>
        public Guid OrderId { get; set; }
        
        [JsonIgnore]
        public Order Order { get; set; } = null!;
        
        /// <summary> Link to the specific <see cref="InventoryItem"/>. Mandatory for all orders. </summary>
        public Guid? InventoryItemId { get; set; }
        
        /// <summary> SKU or code identifying the item (copied from InventoryItem or entered manually). </summary>
        public string ItemCode 
        { 
            get => _itemCode; 
            set { _itemCode = value; OnPropertyChanged(); } 
        }

        /// <summary> Detailed description of the product or service. </summary>
        public string Description 
        { 
            get => _description; 
            set { _description = value; OnPropertyChanged(); } 
        }

        /// <summary> Grouping category (e.g., "Materials", "Labour"). </summary>
        public string Category { get; set; } = "General";
        
        /// <summary> Quantity of units requested. </summary>
        public double QuantityOrdered 
        { 
            get => _quantityOrdered; 
            set 
            { 
                if (_quantityOrdered != value)
                {
                    _quantityOrdered = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(RemainingQuantity)); 
                    OnPropertyChanged(nameof(IsComplete));
                    CalculateTotal(0.15m); // Default VAT 15% for immediate UI update
                }
            } 
        }

        /// <summary> Quantity of units already delivered or fulfilled. </summary>
        public double QuantityReceived { get; set; }

        /// <summary> Unit of measurement (e.g., "kg", "m", "hours"). </summary>
        public string UnitOfMeasure 
        { 
            get => _unitOfMeasure; 
            set { _unitOfMeasure = value; OnPropertyChanged(); } 
        }
        
        /// <summary> Price per single unit (excluding VAT). </summary>
        public decimal UnitPrice 
        { 
            get => _unitPrice; 
            set 
            {
                if (_unitPrice != value)
                {
                    _unitPrice = value; 
                    OnPropertyChanged();
                    CalculateTotal(0.15m); // Default VAT 15%
                }
            } 
        }
        
        /// <summary> Calculated VAT amount for this line. </summary>
        public decimal VatAmount { get; set; } 
        
        /// <summary> Total cost for this line (Quantity * Unit Price) excluding VAT. </summary>
        public decimal LineTotal 
        { 
            get => _lineTotal; 
            set { _lineTotal = value; OnPropertyChanged(); } 
        }

        public string Remarks 
        { 
            get => _remarks; 
            set { _remarks = value; OnPropertyChanged(); } 
        }

        /// <summary> Calculated remaining items to be fulfilled. </summary>
        public double RemainingQuantity => Math.Max(0, QuantityOrdered - QuantityReceived);

        /// <summary> True if the order line has been fully fulfilled. </summary>
        public bool IsComplete => QuantityReceived >= QuantityOrdered;
        
        /// <summary>
        /// Updates the LineTotal and VatAmount based on current quantity and price.
        /// </summary>
        /// <param name="taxRate">The applicable tax rate (e.g., 0.15 for 15%).</param>
        public void CalculateTotal(decimal taxRate)
        {
            decimal qty = (decimal)QuantityOrdered;
            decimal price = UnitPrice;
            
            decimal sub = qty * price;
            VatAmount = sub * taxRate;
            LineTotal = sub; 
        }
    }
}
