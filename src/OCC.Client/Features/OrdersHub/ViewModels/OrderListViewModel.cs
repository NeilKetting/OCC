using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.Client.ViewModels.Core;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    /// <summary>
    /// ViewModel for managing and displaying a list of orders with advanced filtering capabilities by date, branch, and text search.
    /// Supports real-time updates through message subscription.
    /// </summary>
    public partial class OrderListViewModel : ViewModelBase, IRecipient<OCC.Client.ViewModels.Messages.EntityUpdatedMessage>
    {
        #region Private Members

        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly IAuthService _authService;
        private List<OrderSummaryDto> _allOrders = new();

        #endregion

        #region Observables

        /// <summary>
        /// Gets the collection of orders that match the current filter criteria.
        /// </summary>
        public ObservableCollection<OrderSummaryDto> Orders { get; } = new();

        public bool CanImportExport => _authService.CurrentUser?.Email?.Equals("neil@mdk.co.za", StringComparison.OrdinalIgnoreCase) ?? false;

        /// <summary>
        /// Gets or sets the search query used to filter orders by number or supplier name.
        /// </summary>
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        /// <summary>
        /// Gets or sets the currently selected order in the list.
        /// </summary>
        [ObservableProperty]
        private OrderSummaryDto? _selectedOrder;

        /// <summary>
        /// Gets or sets the date-based filter for the order list.
        /// </summary>
        [ObservableProperty]
        private OrderDateFilter _timeFilter = OrderDateFilter.All;

        /// <summary>
        /// Gets or sets the total amount of all orders currently visible in the filtered list.
        /// </summary>
        [ObservableProperty]
        private decimal _filteredTotal;

        /// <summary>
        /// Gets or sets the selected branch filter.
        /// </summary>
        [ObservableProperty]
        private string? _selectedBranchFilter = "All";

        /// <summary>
        /// Gets or sets the selected order status filter.
        /// </summary>
        [ObservableProperty]
        private OrderStatus? _selectedStatusFilter;

        /// <summary>
        /// Gets or sets whether the current filtered list is empty.
        /// </summary>
        [ObservableProperty]
        private bool _isEmpty;

        /// <summary>
        /// Gets the available options for time-based filtering.
        /// </summary>
        public List<string> TimeFilters { get; } = Enum.GetNames(typeof(OrderDateFilter)).Select(f => f.Replace("_", " ")).ToList();
        
        /// <summary>
        /// Gets the available options for branch-based filtering.
        /// </summary>
        public List<string> BranchFilters { get; } = new List<string> { "All" }.Concat(Enum.GetNames(typeof(Branch))).ToList();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderListViewModel"/> class with required dependencies.
        /// </summary>
        /// <param name="orderManager">Central manager for order operations.</param>
        /// <param name="dialogService">Service for displaying user notifications and confirmations.</param>
        public OrderListViewModel(IOrderManager orderManager, IDialogService dialogService, IAuthService authService)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _authService = authService;
            
            // Set default branch filter to user's branch if available
            var userBranch = _authService.CurrentUser?.Branch;
            if (userBranch.HasValue)
            {
                SelectedBranchFilter = userBranch.Value.ToString();
            }

            // Register for Real-time Updates to ensure the list remains synchronized with server changes
            WeakReferenceMessenger.Default.Register(this);
            
            _ = LoadOrders();
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to permanently delete an order from the system.
        /// </summary>
        /// <param name="order">The order to be deleted.</param>
        [RelayCommand]
        public async Task DeleteOrder(OrderSummaryDto order)
        {
            if (order == null) return;

            try 
            {
                BusyText = $"Deleting order {order.OrderNumber}...";
                IsBusy = true;
                await _orderManager.DeleteOrderAsync(order.Id);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    Orders.Remove(order);
                    _allOrders.Remove(order);
                });
            }
            catch(Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Error deleting order: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        /// <summary>
        /// Command to trigger the receiving workflow for a specific order.
        /// </summary>
        /// <param name="order">The order to receive items for.</param>
        [RelayCommand]
        public void RequestReceiveOrder(OrderSummaryDto order)
        {
            if (order == null) return;
            ReceiveOrderRequested?.Invoke(this, order);
        }

        /// <summary>
        /// Command to request viewing detailed information for an order.
        /// </summary>
        /// <param name="order">The order to view.</param>
        [RelayCommand]
        public void ViewOrder(OrderSummaryDto order)
        {
            if (order == null) return;
            ViewOrderRequested?.Invoke(this, order);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Configures the list to show only orders that are ready to be received.
        /// </summary>
        public void SetReceivingMode()
        {
            SearchQuery = string.Empty;
            TimeFilter = OrderDateFilter.All;
            SelectedBranchFilter = "All";
            SelectedStatusFilter = null; // Clear first to ensure refresh if already set to something else
            
            // Filter logic will be handled by FilterOrders which is triggered by property changes
            // Here we want to show Ordered and PartialDelivery
            // Since we only have one status filter property for now, we'll handle this in FilterOrders
            _isReceivingOnly = true;
            FilterOrders();
        }

        private bool _isReceivingOnly;

        public void ClearReceivingMode()
        {
            _isReceivingOnly = false;
            SelectedStatusFilter = null;
            FilterOrders();
        }

        /// <summary>
        /// Asynchronously loads all orders from the Order Manager and applies the current filters.
        /// </summary>
        public async Task LoadOrders()
        {
            if (IsBusy) return;
            try
            {
                BusyText = "Loading orders...";
                IsBusy = true;
                _allOrders = (await _orderManager.GetOrdersAsync()).ToList();
                FilterOrders();
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading orders: {ex.Message}");
                if (_dialogService != null) 
                    await _dialogService.ShowAlertAsync("Error", $"Critical Error loading orders: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Applies text search, date, and branch filters to the full order collection.
        /// </summary>
        private void FilterOrders()
        {
            var query = _allOrders.AsEnumerable();

            // 1. Text Search Filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                query = query.Where(o => o.OrderNumber.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) 
                                      || (o.SupplierName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            // 2. Date/Time Range Filter
            var now = DateTime.Now.Date;
            switch (TimeFilter)
            {
                case OrderDateFilter.Today:
                    query = query.Where(o => o.OrderDate.Date == now);
                    break;
                case OrderDateFilter.Yesterday:
                    query = query.Where(o => o.OrderDate.Date == now.AddDays(-1));
                    break;
                case OrderDateFilter.This_Week:
                    int dayDiff = (int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1; 
                    var startOfWeek = now.AddDays(-dayDiff);
                    var endOfWeek = startOfWeek.AddDays(7);
                    query = query.Where(o => o.OrderDate.Date >= startOfWeek && o.OrderDate.Date < endOfWeek);
                    break;
                 case OrderDateFilter.Last_Week:
                    int dayDiffLast = (int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1;
                    var startOfLastWeek = now.AddDays(-dayDiffLast).AddDays(-7);
                    var endOfLastWeek = startOfLastWeek.AddDays(7);
                    query = query.Where(o => o.OrderDate.Date >= startOfLastWeek && o.OrderDate.Date < endOfLastWeek);
                    break;
                case OrderDateFilter.This_Month:
                    var startOfMonth = new DateTime(now.Year, now.Month, 1);
                    query = query.Where(o => o.OrderDate.Date >= startOfMonth && o.OrderDate.Date < startOfMonth.AddMonths(1));
                    break;
                case OrderDateFilter.Last_Month:
                    var startOfLastMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                    query = query.Where(o => o.OrderDate.Date >= startOfLastMonth && o.OrderDate.Date < startOfLastMonth.AddMonths(1));
                    break;
                case OrderDateFilter.All:
                default:
                    break;
            }

            // 3. Branch Organization Filter
            if (SelectedBranchFilter != "All" && !string.IsNullOrEmpty(SelectedBranchFilter))
            {
                // Branch string comparison (DTO has Branch as string)
                if (Enum.TryParse<Branch>(SelectedBranchFilter, out var branchEnum))
                {
                     query = query.Where(o => o.Branch == branchEnum.ToString());
                }
            }

            // 4. Status Filter
            if (_isReceivingOnly)
            {
                query = query.Where(o => o.Status == OrderStatus.Ordered || o.Status == OrderStatus.PartialDelivery);
            }
            else if (SelectedStatusFilter.HasValue)
            {
                query = query.Where(o => o.Status == SelectedStatusFilter.Value);
            }

            // Sort results by newest first for better visibility
            var result = query.OrderByDescending(o => o.OrderDate).ToList(); 

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                Orders.Clear();
                foreach (var order in result)
                {
                    Orders.Add(order);
                }

                FilteredTotal = result.Sum(o => o.TotalAmount);
                IsEmpty = Orders.Count == 0;
            });
        }

        /// <summary>
        /// Responds to entity update messages to refresh the order list.
        /// </summary>
        /// <param name="message">The notification message indicating an entity has changed.</param>
        public void Receive(OCC.Client.ViewModels.Messages.EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "Order")
            {
                _ = LoadOrders();
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Event raised when a request to receive items for an order is made.
        /// </summary>
        public event EventHandler<OrderSummaryDto>? ReceiveOrderRequested;

        /// <summary>
        /// Event raised when a request to view detailed information about an order is made.
        /// </summary>
        public event EventHandler<OrderSummaryDto>? ViewOrderRequested;

        /// <summary>
        /// Handles changes to the search query by reapplying filters.
        /// </summary>
        partial void OnSearchQueryChanged(string value) => FilterOrders();

        /// <summary>
        /// Responds to changes in the time filter by reapplying filters.
        /// </summary>
        partial void OnTimeFilterChanged(OrderDateFilter value) => FilterOrders();

        /// <summary>
        /// Responds to changes in the branch filter by reapplying filters.
        /// </summary>
        partial void OnSelectedBranchFilterChanged(string? value) => FilterOrders();

        #endregion
    }

    /// <summary>
    /// Defines date range filters for the order list views.
    /// </summary>
    public enum OrderDateFilter
    {
        All,
        Today,
        Yesterday,
        This_Week,
        Last_Week,
        This_Month,
        Last_Month
    }
}

