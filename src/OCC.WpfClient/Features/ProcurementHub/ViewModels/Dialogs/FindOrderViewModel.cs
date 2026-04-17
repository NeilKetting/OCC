using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs
{
    public partial class FindOrderViewModel : ViewModelBase
    {
        private readonly IOrderService _orderService;
        private readonly ISupplierService _supplierService;

        [ObservableProperty]
        private string _transactionType = "Purchase Order";

        public ObservableCollection<string> TransactionTypes { get; } = new() 
        { 
            "Any", "Purchase Order", "Sales Order", "Credit Note" 
        };

        [ObservableProperty]
        private ObservableCollection<Supplier> _suppliers = new();

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private DateTime? _fromDate;

        [ObservableProperty]
        private DateTime? _toDate;

        [ObservableProperty]
        private string _orderNumber = string.Empty;

        [ObservableProperty]
        private decimal? _amount;

        [ObservableProperty]
        private ObservableCollection<Order> _searchResults = new();

        [ObservableProperty]
        private bool _isBusy;

        public event Action? CloseRequested;
        public event Action<Order>? OrderSelected;

        public FindOrderViewModel(IOrderService orderService, ISupplierService supplierService)
        {
            _orderService = orderService;
            _supplierService = supplierService;
            
            _fromDate = DateTime.Today.AddMonths(-1);
            _toDate = DateTime.Today;
            
            LoadSuppliers();
        }

        private async void LoadSuppliers()
        {
            try 
            {
                var suppliers = await _supplierService.GetSuppliersAsync();
                foreach (var s in suppliers) Suppliers.Add(s);
            }
            catch { /* Ignore */ }
        }

        [RelayCommand]
        private async Task FindAsync()
        {
            IsBusy = true;
            try
            {
                var orders = await _orderService.GetOrdersAsync();
                
                // Filter locally for now
                var filtered = orders.Where(o => 
                    (string.IsNullOrEmpty(TransactionType) || TransactionType == "Any" || 
                     (TransactionType == "Purchase Order" && o.OrderType == OrderType.PurchaseOrder)) &&
                    (SelectedSupplier == null || o.SupplierId == SelectedSupplier.Id) &&
                    (!FromDate.HasValue || o.OrderDate >= FromDate) &&
                    (!ToDate.HasValue || o.OrderDate <= ToDate) &&
                    (string.IsNullOrEmpty(OrderNumber) || o.OrderNumber.Contains(OrderNumber, StringComparison.OrdinalIgnoreCase)) &&
                    (!Amount.HasValue || Math.Abs(o.TotalAmount - Amount.Value) < 0.01m)
                ).ToList();

                SearchResults.Clear();
                foreach (var o in filtered) SearchResults.Add(o);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Reset()
        {
            TransactionType = "Purchase Order";
            SelectedSupplier = null;
            FromDate = DateTime.Today.AddMonths(-1);
            ToDate = DateTime.Today;
            OrderNumber = string.Empty;
            Amount = null;
            SearchResults.Clear();
        }

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke();
        }

        [RelayCommand]
        private void GoToOrder(Order order)
        {
            if (order != null)
            {
                OrderSelected?.Invoke(order);
                Close();
            }
        }
    }
}
