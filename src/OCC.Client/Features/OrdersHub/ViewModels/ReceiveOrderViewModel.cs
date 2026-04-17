using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;
using OCC.Client.ViewModels.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    /// <summary>
    /// ViewModel for managing the process of receiving items from a placed order.
    /// Handles quantity validation and updates stock levels via the Order Manager.
    /// </summary>
    public partial class ReceiveOrderViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly ILogger<ReceiveOrderViewModel> _logger;

        #endregion

        #region Observables

        /// <summary>
        /// Gets or sets the order being received.
        /// </summary>
        [ObservableProperty]
        private Order? _order;

        /// <summary>
        /// Gets the collection of line items from the order that can be received.
        /// </summary>
        public ObservableCollection<ReceiveLineItem> OrderItems { get; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether an asynchronous operation is in progress.
        /// </summary>


        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ReceiveOrderViewModel"/> class with required dependencies.
        /// </summary>
        /// <param name="orderManager">Central manager for receiving operations.</param>
        /// <param name="dialogService">Service for user notifications.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public ReceiveOrderViewModel(IOrderManager orderManager, IDialogService dialogService, ILogger<ReceiveOrderViewModel> logger)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _logger = logger;
        }

        /// <summary>
        /// Design-time constructor.
        /// </summary>
        public ReceiveOrderViewModel()
        {
            _orderManager = null!;
            _dialogService = null!;
            _logger = null!;
            _order = new Order 
            { 
                OrderNumber = "PO-2401-001", 
                SupplierName = "Demo Supplier",
                OrderDate = DateTime.Now
            };
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to finalize the receiving process by submitting the quantities to the Order Manager.
        /// </summary>
        [RelayCommand]
        public async Task SubmitReceiving()
        {
            if (Order == null) return;

            var receiveList = OrderItems.Where(i => i.ReceiveNow > 0).ToList();
            if (!receiveList.Any())
            {
                await _dialogService.ShowAlertAsync("Validation", "No items have been marked as received.");
                return;
            }

            foreach (var item in receiveList)
            {
                if (item.ReceiveNow > item.Remaining)
                {
                    await _dialogService.ShowAlertAsync("Over-Receiving", $"Cannot receive {item.ReceiveNow} of '{item.Description}'. Only {item.Remaining} remain.");
                    return;
                }
            }

            try
            {
                BusyText = $"Processing delivery for Order {Order.OrderNumber}...";
                IsBusy = true;

                var updatedLines = receiveList.Select(i => 
                {
                    var line = i.SourceLine;
                    line.QuantityReceived = i.NewTotalReceived;
                    return line;
                }).ToList();

                await _orderManager.ReceiveOrderAsync(Order, updatedLines);

                await _dialogService.ShowAlertAsync("Success", "Successfully processed delivery.");
                OrderReceived?.Invoke(this, EventArgs.Empty);
                Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing receiving for order {OrderId}", Order.Id);
                await _dialogService.ShowAlertAsync("Error", $"An error occurred during receiving: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Automatically populates all line items with their remaining quantity to be received.
        /// </summary>
        [RelayCommand]
        public void ReceiveAll()
        {
            foreach (var item in OrderItems)
            {
                item.ReceiveNow = item.Remaining;
            }
        }

        /// <summary>
        /// Command to cancel the receiving process and close the view.
        /// </summary>
        [RelayCommand]
        public void Cancel()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Loads the specified order and its items into the receiving view.
        /// </summary>
        /// <param name="order">The order to receive.</param>
        public void LoadOrder(Order order)
        {
            Order = order;
            OrderItems.Clear();
            
            if (order.Lines != null)
            {
                foreach (var line in order.Lines)
                {
                    OrderItems.Add(new ReceiveLineItem(line));
                }
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Event raised when the user requests to close the receiving view.
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// Event raised after the order has been successfully processed for receiving.
        /// </summary>
        public event EventHandler? OrderReceived;

        #endregion
    }

    /// <summary>
    /// Helper class for tracking receive state of individual line items.
    /// </summary>
    public partial class ReceiveLineItem : ObservableObject
    {
        private readonly OrderLine _line;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NewTotalReceived))]
        [NotifyPropertyChangedFor(nameof(NewRemaining))]
        private double _receiveNow;

        public ReceiveLineItem(OrderLine line)
        {
            _line = line;
        }

        public OrderLine SourceLine => _line;
        public string Description => _line.Description;
        public string ItemCode => _line.ItemCode;
        public double QuantityOrdered => _line.QuantityOrdered;
        public double QuantityReceivedSoFar => _line.QuantityReceived;
        public double Remaining => Math.Max(0, _line.QuantityOrdered - _line.QuantityReceived);
        public double NewTotalReceived => _line.QuantityReceived + ReceiveNow;
        public double NewRemaining => Math.Max(0, _line.QuantityOrdered - NewTotalReceived);
    }
}
