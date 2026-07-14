using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class ProcurementViewModel : OverlayHostViewModel
    {
        private readonly ILogger<ProcurementViewModel> _logger;
        private readonly INavigationService _navigationService;
        private readonly IOrderService _orderService;
        private readonly ISupplierService _supplierService;
        private readonly IInventoryService _inventoryService;
        private readonly IServiceProvider _serviceProvider;
        private List<Order> _allOrders = new();

        [ObservableProperty]
        private int _activeOrdersCount;

        [ObservableProperty]
        private int _draftOrdersCount;

        [ObservableProperty]
        private int _completedOrdersCount;

        [ObservableProperty]
        private int _allOrdersCount;

        [ObservableProperty]
        private string _selectedCardFilter = "All"; // "Active", "Draft", "Completed", "All"

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _selectedBranchIndex = 0; // 0 = All Branches, 1 = Johannesburg, 2 = Cape Town

        [ObservableProperty]
        private ObservableCollection<Order> _displayOrders = new();

        public ProcurementViewModel(
            ILogger<ProcurementViewModel> logger, 
            INavigationService navigationService,
            IOrderService orderService,
            ISupplierService supplierService,
            IInventoryService inventoryService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _navigationService = navigationService;
            _orderService = orderService;
            _supplierService = supplierService;
            _inventoryService = inventoryService;
            _serviceProvider = serviceProvider;
            Title = "Procurement Overview";
            
            _logger.LogInformation("ProcurementViewModel initialized");
            _ = LoadDashboardDataAsync();
        }

        protected override void OnActiveHubChanged(bool isActive)
        {
            if (isActive)
            {
                _ = LoadDashboardDataAsync();
            }
        }

        [RelayCommand]
        public async Task LoadDashboardDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading procurement dashboard data...";

                var ordersTask = _orderService.GetOrdersAsync();
                var inventoryTask = _inventoryService.GetInventoryAsync();
                var suppliersTask = _supplierService.GetSupplierSummariesAsync();

                await Task.WhenAll(ordersTask, inventoryTask, suppliersTask);

                var orders = await ordersTask;
                var inventory = await inventoryTask;
                
                _allOrders = orders.OrderByDescending(o => o.OrderDate).ToList();

                // Calculate counts
                ActiveOrdersCount = _allOrders.Count(o => o.Status == OrderStatus.Ordered || o.Status == OrderStatus.PartialDelivery);
                DraftOrdersCount = _allOrders.Count(o => o.Status == OrderStatus.Draft);
                CompletedOrdersCount = _allOrders.Count(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.Finalised);
                AllOrdersCount = _allOrders.Count;

                FilterOrders();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading procurement dashboard data");
                NotifyError("Error", "Could not load procurement overview data.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void FilterByCard(string filter)
        {
            SelectedCardFilter = filter;
            FilterOrders();
        }

        private void FilterOrders()
        {
            var filtered = _allOrders.AsEnumerable();

            // 1. Filter by Stats Card selection
            filtered = SelectedCardFilter switch
            {
                "Active" => filtered.Where(o => o.Status == OrderStatus.Ordered || o.Status == OrderStatus.PartialDelivery),
                "Draft" => filtered.Where(o => o.Status == OrderStatus.Draft),
                "Completed" => filtered.Where(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.Finalised),
                _ => filtered
            };

            // 2. Filter by Branch Selection (0 = All, 1 = JHB, 2 = CPT)
            filtered = SelectedBranchIndex switch
            {
                1 => filtered.Where(o => o.Branch == Branch.JHB),
                2 => filtered.Where(o => o.Branch == Branch.CPT),
                _ => filtered
            };

            // 3. Filter by Search Query (Order #, Supplier name, or Project name)
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(o =>
                    (o.OrderNumber?.ToLower().Contains(query) ?? false) ||
                    (o.SupplierName?.ToLower().Contains(query) ?? false) ||
                    (o.ProjectName?.ToLower().Contains(query) ?? false));
            }

            DisplayOrders.Clear();
            foreach (var order in filtered)
            {
                DisplayOrders.Add(order);
            }
        }

        partial void OnSearchQueryChanged(string value) => FilterOrders();
        partial void OnSelectedBranchIndexChanged(int value) => FilterOrders();

        [RelayCommand]
        private void OpenOrder(Order order)
        {
            if (order != null)
            {
                WeakReferenceMessenger.Default.Send(new OpenOrderMessage(order.Id));
            }
        }

        [RelayCommand]
        private void ReceiveOrderStock(Order order)
        {
            if (order != null)
            {
                ShowReceiveStockDialog(order);
            }
        }

        [RelayCommand]
        private void NavigateToPurchaseOrder()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.PurchaseOrder));
        }

        [RelayCommand]
        private void NavigateToPicking()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Picking));
        }

        [RelayCommand]
        private void ReceiveStock()
        {
            var findOrderVm = new FindOrderViewModel(_orderService, _supplierService);
            findOrderVm.CloseRequested += CloseOverlay;
            findOrderVm.OrderSelected += (order) =>
            {
                CloseOverlay();
                ShowReceiveStockDialog(order);
            };
            OpenOverlay(findOrderVm);
        }

        private void ShowReceiveStockDialog(Order order)
        {
            var receiveVm = (Dialogs.ReceiveStockViewModel)System.Windows.Application.Current.Dispatcher.Invoke(() => _serviceProvider.GetRequiredService<Dialogs.ReceiveStockViewModel>());
            receiveVm.LoadOrder(order);
            receiveVm.CloseRequested += CloseOverlay;
            receiveVm.OrderReceived += async () => 
            {
                await LoadDashboardDataAsync();
            };
            OpenOverlay(receiveVm);
        }
    }
}
