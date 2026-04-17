using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.Client.ViewModels.Core;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.ViewModels.Messages;
using OCC.Client.Messages;
using OCC.Client.Models;
using OCC.Client.Services.Infrastructure;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    /// <summary>
    /// ViewModel for the Order Dashboard, providing a high-level overview of order activities,
    /// pending deliveries, and inventory alerts.
    /// </summary>
    public partial class OrderDashboardViewModel : ViewModelBase, IRecipient<EntityUpdatedMessage>
    {
        #region Private Members

        private readonly IOrderManager _orderManager;
        private readonly OrderStateService _orderStateService;
        private readonly IAuthService _authService;
        private readonly ILogger<OrderDashboardViewModel> _logger;

        #endregion

        #region Observables

        /// <summary>
        /// Gets or sets the total number of orders placed in the current month.
        /// </summary>
        [ObservableProperty]
        private int _ordersThisMonth;
        
        /// <summary>
        /// Gets or sets the count of pending deliveries.
        /// </summary>
        [ObservableProperty]
        private int _pendingDeliveries;
        
        /// <summary>
        /// Gets or sets the count of items currently below their reorder point.
        /// </summary>
        [ObservableProperty]
        private int _lowStockItemsCount;

        /// <summary>
        /// Gets or sets the growth description text (e.g., "+5% from last month").
        /// </summary>
        [ObservableProperty]
        private string _monthGrowthText = string.Empty;

        /// <summary>
        /// Gets or sets the color for the growth text based on positive or negative change.
        /// </summary>
        [ObservableProperty]
        private string _monthGrowthColor = "Green";

        /// <summary>
        /// Gets or sets the descriptive text for the next pending delivery.
        /// </summary>
        [ObservableProperty]
        private string _pendingDeliveryText = string.Empty;

        /// <summary>
        /// Gets or sets the color for the pending delivery status.
        /// </summary>
        [ObservableProperty]
        private string _pendingDeliveryColor = "Orange";

        /// <summary>
        /// Gets the collection of recently placed orders.
        /// </summary>
        public ObservableCollection<OrderSummaryDto> RecentOrders { get; } = new();

        /// <summary>
        /// Gets the collection of low stock items requiring attention.
        /// </summary>
        public ObservableCollection<RestockCandidateDto> LowStockItems { get; } = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderDashboardViewModel"/> class for design-time support.
        /// </summary>
        public OrderDashboardViewModel()
        {
            _orderManager = null!;
            _orderStateService = null!;
            _authService = null!;
            _logger = null!;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderDashboardViewModel"/> class with required dependencies.
        /// </summary>
        /// <param name="orderManager">Manager providing centralized order and inventory operations.</param>
        /// <param name="authService">Service for accessing current user context.</param>
        /// <param name="logger">Logger for capturing diagnostic information.</param>
        public OrderDashboardViewModel(IOrderManager orderManager, OrderStateService orderStateService, IAuthService authService, ILogger<OrderDashboardViewModel> logger)
        {
            _orderManager = orderManager;
            _orderStateService = orderStateService;
            _authService = authService;
            _logger = logger;
            
            // Register for Real-time Updates to keep dashboard current
            WeakReferenceMessenger.Default.Register(this);

            _ = LoadData(); 
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void RestockNow()
        {
             // New Flow: Review page -> Create Order
             WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("RestockReview"));
        }

        /// <summary>
        /// Event raised when an order is selected from the dashboard for detailed viewing.
        /// </summary>
        public event EventHandler<OrderSummaryDto>? OrderSelected;

        [RelayCommand]
        private void OpenOrder(OrderSummaryDto order)
        {
            if (order == null) return;
            
            // Invoke event for the parent ViewModel to handle navigation/loading
            OrderSelected?.Invoke(this, order);
        }

        [RelayCommand]
        private void NavigateToOrderList()
        {
             WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("OrderList"));
        }

        [RelayCommand]
        private void NavigateToReceiving()
        {
             WeakReferenceMessenger.Default.Send(new NavigationRequestMessage(OCC.Client.Infrastructure.NavigationRoutes.Receiving));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Asynchronously loads dashboard statistics and alerts from the Order Manager.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LoadData()
        {
            try
            {
                IsBusy = true;
                
                // Fetch branch-isolated stats
                var branch = _authService?.CurrentUser?.Branch;
                var stats = await _orderManager.GetDashboardStatsAsync(branch);

                // Update Properties
                OrdersThisMonth = stats.OrdersThisMonth;
                MonthGrowthText = stats.MonthGrowthText;
                MonthGrowthColor = stats.MonthGrowthColor;
                
                PendingDeliveries = stats.PendingDeliveriesCount;
                PendingDeliveryText = stats.PendingDeliveriesText;
                PendingDeliveryColor = stats.PendingDeliveriesColor;

                LowStockItemsCount = stats.LowStockCount;

                // Update Collections
                RecentOrders.Clear();
                foreach (var o in stats.RecentOrders) RecentOrders.Add(o);

                LowStockItems.Clear();
                foreach (var i in stats.LowStockItems) LowStockItems.Add(i);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Handles real-time updates for orders and inventory items to refresh the dashboard.
        /// </summary>
        /// <param name="message">The update message containing entity information.</param>
        public void Receive(EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "Order" || message.Value.EntityType == "Inventory")
            {
                // Refresh dashboard if Orders or Inventory changes
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => await LoadData());
            }
        }

        #endregion
    }
}
