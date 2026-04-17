using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace OCC.Client.ModelWrappers
{
    public partial class OrderWrapper : ObservableValidator
    {
        private readonly Order _model;

        public OrderWrapper(Order model)
        {
            _model = model;
            Initialize();
            
            // Listen to collection changes
            Lines.CollectionChanged += Lines_CollectionChanged;
            
            // Listen to property changes of items in the collection
            foreach (var line in Lines)
            {
                line.PropertyChanged += OrderLine_PropertyChanged;
            }
        }

        public Order Model => _model;

        public Guid Id => _model.Id;

        [ObservableProperty]
        private string _orderNumber = string.Empty;

        [ObservableProperty]
        private DateTime _orderDate;

        [ObservableProperty]
        [Required(ErrorMessage = "Expected delivery date is required")]
        private DateTime? _expectedDeliveryDate;

        [ObservableProperty]
        private OrderStatus _status;

        [ObservableProperty]
        private OrderType _orderType;

        [ObservableProperty]
        private Guid? _supplierId;

        [ObservableProperty]
        private Guid? _customerId;

        [ObservableProperty]
        private Guid? _projectId;

        [ObservableProperty]
        private OrderDestinationType _destinationType;

        [ObservableProperty]
        private string _attention = string.Empty;

        [ObservableProperty]
        private decimal _taxRate = 0.15m;

        [ObservableProperty]
        private string _supplierName = string.Empty;

        [ObservableProperty]
        private string _entityAddress = string.Empty;

        [ObservableProperty]
        private string _entityTel = string.Empty;

        [ObservableProperty]
        private string _entityVatNo = string.Empty;

        [ObservableProperty]
        private string _projectName = string.Empty;

        [ObservableProperty]
        private Branch _branch;

        [ObservableProperty]
        private string _deliveryInstructions = string.Empty;

        [ObservableProperty]
        private string _notes = string.Empty;

        [ObservableProperty]
        private string _scopeOfWork = string.Empty;

        // Totals
        [ObservableProperty]
        private decimal _subTotal;

        [ObservableProperty]
        private decimal _vatTotal;

        [ObservableProperty]
        private decimal _totalAmount;

        public ObservableCollection<OrderLineWrapper> Lines { get; } = new();

        private void Initialize()
        {
            OrderNumber = _model.OrderNumber;
            OrderDate = _model.OrderDate;
            ExpectedDeliveryDate = _model.ExpectedDeliveryDate;
            Status = _model.Status;
            OrderType = _model.OrderType;
            SupplierId = _model.SupplierId;
            CustomerId = _model.CustomerId;
            ProjectId = _model.ProjectId;
            DestinationType = _model.DestinationType;
            Attention = _model.Attention ?? string.Empty;
            TaxRate = _model.TaxRate;
            SupplierName = _model.SupplierName;
            EntityAddress = _model.EntityAddress;
            EntityTel = _model.EntityTel;
            EntityVatNo = _model.EntityVatNo;
            ProjectName = _model.ProjectName ?? string.Empty;
            Branch = _model.Branch;
            DeliveryInstructions = _model.DeliveryInstructions ?? string.Empty;
            Notes = _model.Notes ?? string.Empty;
            ScopeOfWork = _model.ScopeOfWork ?? string.Empty;

            foreach (var line in _model.Lines)
            {
                Lines.Add(new OrderLineWrapper(line));
            }
            
            // Initial calc
            CalculateTotals();
        }

        public void CommitToModel()
        {
            _model.OrderNumber = OrderNumber;
            _model.OrderDate = OrderDate;
            _model.ExpectedDeliveryDate = ExpectedDeliveryDate;
            _model.Status = Status;
            _model.OrderType = OrderType;
            _model.SupplierId = SupplierId;
            _model.CustomerId = CustomerId;
            _model.ProjectId = ProjectId;
            _model.DestinationType = DestinationType;
            _model.Attention = Attention;
            _model.TaxRate = TaxRate;
            _model.SupplierName = SupplierName;
            _model.EntityAddress = EntityAddress;
            _model.EntityTel = EntityTel;
            _model.EntityVatNo = EntityVatNo;
            _model.ProjectName = ProjectName;
            _model.Branch = Branch;
            _model.DeliveryInstructions = DeliveryInstructions;
            _model.Attention = Attention;
            _model.Notes = Notes;
            _model.ScopeOfWork = ScopeOfWork;

            // Sync lines back to model
            _model.Lines.Clear();
            foreach (var wrapper in Lines)
            {
                // Only save lines that have at least a description or item code
                if (!string.IsNullOrWhiteSpace(wrapper.Description) || !string.IsNullOrWhiteSpace(wrapper.ItemCode))
                {
                    wrapper.CommitToModel();
                    _model.Lines.Add(wrapper.Model);
                }
            }
            
            // Sync totals to model - REMOVED assignments to read-only properties
            // The model calculates these from lines, so just syncing lines is sufficient.
        }

        partial void OnExpectedDeliveryDateChanged(DateTime? value)
        {
            ValidateProperty(value, nameof(ExpectedDeliveryDate));
            if (value.HasValue && value.Value.Date < DateTime.Today && Status != OrderStatus.Completed && Status != OrderStatus.Cancelled)
            {
                // We add a custom error if date is in past for active orders
                // Note: ObservableValidator usually handles attributes, but custom logic validation 
                // requires adding errors manually or using CustomValidationAttribute.
                // For now, we will stick to the basic Required check here and let VM handle the "Past Date" alert if they want strict blocking,
                // OR we can move the "Past Date" logic here.
                // Let's rely on VM for the strict "blocking" alert for now to replicate existing behavior,
                // but we could change the property to be invalid.
            }
        }

        private void Lines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (OrderLineWrapper item in e.NewItems)
                    item.PropertyChanged += OrderLine_PropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (OrderLineWrapper item in e.OldItems)
                    item.PropertyChanged -= OrderLine_PropertyChanged;
            }
            CalculateTotals();
        }

        private void OrderLine_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OrderLineWrapper.LineTotal) || 
                e.PropertyName == nameof(OrderLineWrapper.VatAmount))
            {
                CalculateTotals();
            }
        }

        public void Validate() => ValidateAllProperties();

        private void CalculateTotals()
        {
            SubTotal = Lines.Sum(l => l.LineTotal);
            VatTotal = Lines.Sum(l => l.VatAmount);
            TotalAmount = SubTotal + VatTotal;
        }
    }
}
