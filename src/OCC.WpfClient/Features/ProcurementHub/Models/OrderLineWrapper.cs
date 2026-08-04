using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using System;

namespace OCC.WpfClient.Features.ProcurementHub.Models
{
    /// <summary>
    /// WPF presentation wrapper around an <see cref="OrderLine"/> domain model.
    /// This class is the sole owner of <see cref="INotifyPropertyChanged"/> for line items.
    /// The underlying <see cref="OrderLine"/> model is intentionally INPC-free.
    /// </summary>
    public class OrderLineWrapper : ViewModelBase
    {
        private readonly OrderWrapper _parent;

        /// <summary>The underlying domain model this wrapper represents.</summary>
        public OrderLine Model { get; }

        /// <summary>
        /// Tracks the last SKU for which validation was run. Used to suppress the
        /// "Item not found" dialog from firing repeatedly for the same typed code.
        /// </summary>
        public string? LastValidatedSku { get; set; }

        public OrderLineWrapper(OrderLine model, OrderWrapper parent)
        {
            Model = model;
            _parent = parent;
        }

        // ─── Identity ─────────────────────────────────────────────────────────────

        /// <summary>The unique identifier of the underlying order line.</summary>
        public Guid Id => Model.Id;

        // ─── SKU / Item Fields ────────────────────────────────────────────────────

        /// <summary>
        /// The inventory item ID linked to this line. When set alongside a valid
        /// <see cref="ItemCode"/>, <see cref="IsItemValid"/> becomes true, which
        /// unlocks the QTY / UNIT / PRICE / AMOUNT columns in the XAML DataGrid.
        /// </summary>
        public Guid? InventoryItemId
        {
            get => Model.InventoryItemId;
            set
            {
                Model.InventoryItemId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsItemValid));
            }
        }

        /// <summary>
        /// The SKU / item code for this line. Typed by the user or auto-filled when
        /// an inventory item is selected from the dropdown. Triggers
        /// <see cref="IsItemValid"/> notification on change.
        /// </summary>
        public string ItemCode
        {
            get => Model.ItemCode;
            set
            {
                Model.ItemCode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsItemValid));
            }
        }

        public string Description
        {
            get => Model.Description;
            set { Model.Description = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Grouping category for this line (e.g., "Materials", "Labour", "General").
        /// Defaults to "General" on the model if not explicitly set.
        /// </summary>
        public string Category
        {
            get => Model.Category;
            set { Model.Category = value; OnPropertyChanged(); }
        }

        // ─── Quantity / Pricing ───────────────────────────────────────────────────

        public double QuantityOrdered
        {
            get => Model.QuantityOrdered;
            set
            {
                Model.QuantityOrdered = value;
                OnPropertyChanged();
                UpdateCalculations();
            }
        }

        public string UnitOfMeasure
        {
            get => Model.UnitOfMeasure;
            set { Model.UnitOfMeasure = value; OnPropertyChanged(); }
        }

        public decimal UnitPrice
        {
            get => Model.UnitPrice;
            set
            {
                Model.UnitPrice = value;
                OnPropertyChanged();
                UpdateCalculations();
            }
        }

        public string Remarks
        {
            get => Model.Remarks;
            set { Model.Remarks = value; OnPropertyChanged(); }
        }

        // ─── Computed / Read-only ─────────────────────────────────────────────────

        public decimal LineTotal => Model.LineTotal;
        public decimal VatAmount => Model.VatAmount;

        /// <summary>
        /// True when both <see cref="ItemCode"/> is non-empty and
        /// <see cref="InventoryItemId"/> is set. Controls visibility of QTY, UNIT,
        /// UNIT PRICE, and AMOUNT columns via XAML DataTriggers.
        /// If this is false for a line with data, it indicates the inventory lookup
        /// did not resolve — check that InventoryItems was loaded before the order lines
        /// were populated.
        /// </summary>
        public bool IsItemValid => !string.IsNullOrWhiteSpace(ItemCode) && InventoryItemId.HasValue;

        /// <summary>
        /// Recalculates <see cref="LineTotal"/> and <see cref="VatAmount"/> using the
        /// parent order's <see cref="OrderWrapper.TaxRate"/>, then notifies the parent
        /// to update footer totals.
        /// </summary>
        public void UpdateCalculations()
        {
            Model.CalculateTotal(_parent.TaxRate);
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(VatAmount));
            _parent.NotifyTotals();
        }
    }
}
