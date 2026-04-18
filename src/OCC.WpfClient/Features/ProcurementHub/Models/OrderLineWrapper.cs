using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using System;

namespace OCC.WpfClient.Features.ProcurementHub.Models
{
    public class OrderLineWrapper : ViewModelBase
    {
        private readonly OrderWrapper _parent;
        public OrderLine Model { get; }

        public OrderLineWrapper(OrderLine model, OrderWrapper parent)
        {
            Model = model;
            _parent = parent;
        }

        public string? LastValidatedSku { get; set; }

        public Guid Id => Model.Id;

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

        public decimal LineTotal => Model.LineTotal;
        public decimal VatAmount => Model.VatAmount;

        public bool IsItemValid => !string.IsNullOrWhiteSpace(ItemCode) && InventoryItemId.HasValue;

        public void UpdateCalculations()
        {
            Model.CalculateTotal(_parent.TaxRate);
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(VatAmount));
            _parent.NotifyTotals();
        }
    }
}
