using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace OCC.WpfClient.Features.ProcurementHub.Models
{
    /// <summary>
    /// WPF presentation wrapper around an <see cref="Order"/> domain model.
    /// Exposes <see cref="INotifyPropertyChanged"/> properties and an
    /// <see cref="ObservableCollection{T}"/> of <see cref="OrderLineWrapper"/> for
    /// UI data binding. Keeps the underlying <see cref="Model"/> in sync.
    /// </summary>
    public class OrderWrapper : ViewModelBase
    {
        /// <summary>The underlying domain model this wrapper represents.</summary>
        public Order Model { get; }

        /// <summary>
        /// When true, the CollectionChanged handler suppresses syncing back to
        /// <see cref="Model"/>.Lines. Used during construction to prevent double-add,
        /// since <see cref="Model"/>.Lines is already populated when we wrap it.
        /// </summary>
        private bool _suppressModelSync;

        /// <summary>
        /// Initialises the wrapper from an existing or newly created <see cref="Order"/>.
        /// </summary>
        public OrderWrapper(Order model)
        {
            Model = model;

            // Suppress sync during construction — model.Lines already contains these lines.
            // Without suppression, OnLinesCollectionChanged would re-add each model line,
            // causing duplicates in Model.Lines (the double-add bug).
            _suppressModelSync = true;
            Lines = new ObservableCollection<OrderLineWrapper>(
                model.Lines.Select(l => new OrderLineWrapper(l, this))
            );
            _suppressModelSync = false;

            Lines.CollectionChanged += OnLinesCollectionChanged;
        }

        // ─── Identity ────────────────────────────────────────────────────────────

        /// <summary>The unique identifier of the underlying order.</summary>
        public Guid Id => Model.Id;

        // ─── Header Fields ────────────────────────────────────────────────────────

        public string OrderNumber
        {
            get => Model.OrderNumber;
            set { Model.OrderNumber = value; OnPropertyChanged(); }
        }

        public DateTime OrderDate
        {
            get => Model.OrderDate;
            set { Model.OrderDate = value; OnPropertyChanged(); }
        }

        public DateTime? ExpectedDeliveryDate
        {
            get => Model.ExpectedDeliveryDate;
            set { Model.ExpectedDeliveryDate = value; OnPropertyChanged(); }
        }

        public Branch Branch
        {
            get => Model.Branch;
            set { Model.Branch = value; OnPropertyChanged(); }
        }

        // ─── Supplier Fields ──────────────────────────────────────────────────────

        public Guid? SupplierId
        {
            get => Model.SupplierId;
            set { Model.SupplierId = value; OnPropertyChanged(); }
        }

        public string SupplierName
        {
            get => Model.SupplierName;
            set { Model.SupplierName = value; OnPropertyChanged(); }
        }

        public string EntityAddress
        {
            get => Model.EntityAddress;
            set { Model.EntityAddress = value; OnPropertyChanged(); }
        }

        public string EntityTel
        {
            get => Model.EntityTel;
            set { Model.EntityTel = value; OnPropertyChanged(); }
        }

        public string EntityVatNo
        {
            get => Model.EntityVatNo;
            set { Model.EntityVatNo = value; OnPropertyChanged(); }
        }

        // ─── Project / Logistics Fields ───────────────────────────────────────────

        public Guid? ProjectId
        {
            get => Model.ProjectId;
            set { Model.ProjectId = value; OnPropertyChanged(); }
        }

        public string? ProjectName
        {
            get => Model.ProjectName;
            set { Model.ProjectName = value; OnPropertyChanged(); }
        }

        public string Attention
        {
            get => Model.Attention;
            set { Model.Attention = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Delivery destination type. Toggling this notifies the three boolean helpers
        /// (<see cref="IsSiteSelected"/>, <see cref="IsOfficeSelected"/>, <see cref="IsOtherSelected"/>)
        /// so XAML radio buttons stay in sync without converter tricks.
        /// </summary>
        public OrderDestinationType DestinationType
        {
            get => Model.DestinationType;
            set
            {
                Model.DestinationType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSiteSelected));
                OnPropertyChanged(nameof(IsOfficeSelected));
                OnPropertyChanged(nameof(IsOtherSelected));
            }
        }

        public bool IsSiteSelected
        {
            get => DestinationType == OrderDestinationType.Site;
            set { if (value) DestinationType = OrderDestinationType.Site; }
        }

        public bool IsOfficeSelected
        {
            get => DestinationType == OrderDestinationType.Stock;
            set { if (value) DestinationType = OrderDestinationType.Stock; }
        }

        public bool IsOtherSelected
        {
            get => DestinationType == OrderDestinationType.Other;
            set { if (value) DestinationType = OrderDestinationType.Other; }
        }

        // ─── Note / Instruction Fields ────────────────────────────────────────────

        public string ScopeOfWork
        {
            get => Model.ScopeOfWork;
            set { Model.ScopeOfWork = value; OnPropertyChanged(); }
        }

        public string DeliveryInstructions
        {
            get => Model.DeliveryInstructions;
            set { Model.DeliveryInstructions = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Mapped to <see cref="Order.Notes"/> which stores the free-form delivery address
        /// when <see cref="IsOtherSelected"/> is true.
        /// </summary>
        public string DeliveryAddress
        {
            get => Model.Notes;
            set { Model.Notes = value; OnPropertyChanged(); }
        }

        public string Template
        {
            get => Model.Template;
            set { Model.Template = value; OnPropertyChanged(); }
        }

        public string Terms
        {
            get => Model.Terms;
            set { Model.Terms = value; OnPropertyChanged(); }
        }

        public string ReferenceNo
        {
            get => Model.ReferenceNo;
            set { Model.ReferenceNo = value; OnPropertyChanged(); }
        }

        // ─── Financials ───────────────────────────────────────────────────────────

        public decimal TaxRate
        {
            get => Model.TaxRate;
            set
            {
                Model.TaxRate = value;
                OnPropertyChanged();
                UpdateTotals();
            }
        }

        // ─── Lines ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Observable collection of line wrappers used for UI data binding.
        /// Kept in sync with <see cref="Model"/>.Lines via <see cref="OnLinesCollectionChanged"/>.
        /// </summary>
        public ObservableCollection<OrderLineWrapper> Lines { get; }

        // ─── Totals (computed) ────────────────────────────────────────────────────

        public decimal SubTotal => Lines.Sum(l => l.LineTotal);
        public decimal VatTotal => Lines.Sum(l => l.VatAmount);
        public decimal TotalAmount => SubTotal + VatTotal;

        /// <summary>
        /// Recalculates all line totals then notifies the header total properties.
        /// Called when <see cref="TaxRate"/> changes.
        /// </summary>
        public void UpdateTotals()
        {
            foreach (var line in Lines)
            {
                line.UpdateCalculations();
            }
            NotifyTotals();
        }

        /// <summary>
        /// Raises PropertyChanged for all three total computed properties so the UI
        /// footer updates without binding each line individually.
        /// </summary>
        public void NotifyTotals()
        {
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(VatTotal));
            OnPropertyChanged(nameof(TotalAmount));
        }

        // ─── Sync Handler ──────────────────────────────────────────────────────────

        /// <summary>
        /// Keeps <see cref="Model"/>.Lines in sync with the <see cref="Lines"/> wrapper
        /// collection. Suppressed during construction to avoid double-adding lines that
        /// were already present on <see cref="Model"/>.Lines.
        /// </summary>
        private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressModelSync) return;

            if (e.NewItems != null)
            {
                foreach (OrderLineWrapper item in e.NewItems)
                {
                    // Only add to Model.Lines if not already present (safety guard).
                    if (!Model.Lines.Contains(item.Model))
                    {
                        Model.Lines.Add(item.Model);
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (OrderLineWrapper item in e.OldItems)
                {
                    Model.Lines.Remove(item.Model);
                }
            }

            NotifyTotals();
        }
    }
}
