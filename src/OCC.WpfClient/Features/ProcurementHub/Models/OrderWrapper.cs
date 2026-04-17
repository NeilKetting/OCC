using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace OCC.WpfClient.Features.ProcurementHub.Models
{
    public class OrderWrapper : ViewModelBase
    {
        public Order Model { get; }

        public OrderWrapper(Order model)
        {
            Model = model;
            Lines = new ObservableCollection<OrderLineWrapper>(
                model.Lines.Select(l => new OrderLineWrapper(l, this))
            );
            Lines.CollectionChanged += OnLinesCollectionChanged;
        }

        public Guid Id => Model.Id;

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

        public string Attention
        {
            get => Model.Attention;
            set { Model.Attention = value; OnPropertyChanged(); }
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

        public OrderDestinationType DestinationType
        {
            get => Model.DestinationType;
            set 
            { 
                Model.DestinationType = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsSiteSelected));
                OnPropertyChanged(nameof(IsOfficeSelected));
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

        public ObservableCollection<OrderLineWrapper> Lines { get; }

        public decimal SubTotal => Lines.Sum(l => l.LineTotal);
        public decimal VatTotal => Lines.Sum(l => l.VatAmount);
        public decimal TotalAmount => SubTotal + VatTotal;

        public void UpdateTotals()
        {
            foreach (var line in Lines)
            {
                line.UpdateCalculations();
            }
            NotifyTotals();
        }

        public void NotifyTotals()
        {
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(VatTotal));
            OnPropertyChanged(nameof(TotalAmount));
        }

        private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (OrderLineWrapper item in e.NewItems)
                {
                    Model.Lines.Add(item.Model);
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
