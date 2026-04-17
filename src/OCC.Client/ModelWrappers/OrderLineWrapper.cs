using CommunityToolkit.Mvvm.ComponentModel;

using OCC.Shared.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Client.ModelWrappers
{
    public partial class OrderLineWrapper : ObservableValidator
    {
        private readonly OrderLine _model;

        public OrderLineWrapper(OrderLine model)
        {
            _model = model;
            Initialize();
        }

        public OrderLine Model => _model;

        public Guid Id => _model.Id;
        public Guid OrderId => _model.OrderId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPopulated))]
        private string _itemCode = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Description is required")]
        [NotifyPropertyChangedFor(nameof(IsPopulated))]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _category = "General";

        [ObservableProperty]
        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        [NotifyPropertyChangedFor(nameof(IsPopulated))]
        private double _quantityOrdered;

        [ObservableProperty]
        private double _quantityReceived;

        [ObservableProperty]
        private string _unitOfMeasure = string.Empty;

        [ObservableProperty]
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        [NotifyPropertyChangedFor(nameof(IsPopulated))]
        private decimal _unitPrice;

        [ObservableProperty]
        private decimal _vatAmount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPopulated))]
        private decimal _lineTotal;

        public bool IsPopulated => !string.IsNullOrWhiteSpace(ItemCode) || !string.IsNullOrWhiteSpace(Description) || LineTotal > 0;

        public Guid? InventoryItemId
        {
            get => _model.InventoryItemId;
            set => _model.InventoryItemId = value;
        }

        private void Initialize()
        {
            ItemCode = _model.ItemCode;
            Description = _model.Description;
            Category = _model.Category;
            QuantityOrdered = _model.QuantityOrdered;
            QuantityReceived = _model.QuantityReceived;
            UnitOfMeasure = _model.UnitOfMeasure;
            UnitPrice = _model.UnitPrice;
            VatAmount = _model.VatAmount;
            LineTotal = _model.LineTotal;
            OnPropertyChanged(nameof(IsPopulated));
        }

        public void CommitToModel()
        {
            _model.ItemCode = ItemCode;
            _model.Description = Description;
            _model.Category = Category;
            _model.QuantityOrdered = QuantityOrdered;
            _model.QuantityReceived = QuantityReceived;
            _model.UnitOfMeasure = UnitOfMeasure;
            _model.UnitPrice = UnitPrice;
            _model.VatAmount = VatAmount;
            _model.LineTotal = LineTotal;
        }

        partial void OnQuantityOrderedChanged(double value)
        {
            ValidateProperty(value, nameof(QuantityOrdered));
            CalculateTotal();
        }

        partial void OnUnitPriceChanged(decimal value)
        {
            ValidateProperty(value, nameof(UnitPrice));
            CalculateTotal();
        }

        partial void OnDescriptionChanged(string value) => ValidateProperty(value, nameof(Description));

        public void CalculateTotal(decimal taxRate)
        {
            decimal qty = (decimal)QuantityOrdered;
            decimal price = UnitPrice;

            decimal sub = qty * price;
            VatAmount = sub * taxRate;
            LineTotal = sub; 
        }

        private void CalculateTotal() => CalculateTotal(0.15m);
    }
}
