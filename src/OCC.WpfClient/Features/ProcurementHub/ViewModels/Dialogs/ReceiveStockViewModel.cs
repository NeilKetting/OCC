using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
    public partial class ReceiveStockViewModel : ViewModelBase
    {
        private readonly IOrderService _orderService;
        private readonly IToastService _toastService;
        private readonly ILogger<ReceiveStockViewModel> _logger;

        [ObservableProperty]
        private Order? _order;

        [ObservableProperty]
        private ObservableCollection<ReceiveLineItemViewModel> _orderItems = new();

        public event Action? CloseRequested;
        public event Action? OrderReceived;

        public ReceiveStockViewModel(
            IOrderService orderService, 
            IToastService toastService, 
            ILogger<ReceiveStockViewModel> logger)
        {
            _orderService = orderService;
            _toastService = toastService;
            _logger = logger;
            Title = "Receive Stock";
        }

        public void LoadOrder(Order order)
        {
            Order = order;
            OrderItems.Clear();
            
            if (order.Lines != null)
            {
                foreach (var line in order.Lines)
                {
                    OrderItems.Add(new ReceiveLineItemViewModel(line));
                }
            }
            
            Title = $"Receive Order: {order.OrderNumber}";
        }

        [RelayCommand]
        public void ReceiveAll()
        {
            foreach (var item in OrderItems)
            {
                item.ReceiveNow = item.Remaining;
            }
        }

        [RelayCommand]
        public async Task SubmitReceiving()
        {
            if (Order == null) return;

            var receiveList = OrderItems.Where(i => i.ReceiveNow > 0).ToList();
            if (!receiveList.Any())
            {
                _toastService.ShowWarning("Validation", "No items have been marked as received.");
                return;
            }

            foreach (var item in receiveList)
            {
                if (item.ReceiveNow > item.Remaining)
                {
                    _toastService.ShowWarning("Over-Receiving", $"Cannot receive {item.ReceiveNow} of '{item.Description}'. Only {item.Remaining} remain.");
                    return;
                }
            }

            try
            {
                IsBusy = true;
                BusyText = $"Processing delivery for Order {Order.OrderNumber}...";

                var updatedLines = receiveList.Select(i => 
                {
                    var line = i.SourceLine;
                    line.QuantityReceived = i.NewTotalReceived;
                    return line;
                }).ToList();

                var result = await _orderService.ReceiveOrderAsync(Order.Id, updatedLines);

                if (result != null)
                {
                    _toastService.ShowSuccess("Success", "Successfully processed delivery.");
                    OrderReceived?.Invoke();
                    Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing receiving for order {OrderId}", Order.Id);
                _toastService.ShowError("Error", $"An error occurred during receiving: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Close()
        {
            CloseRequested?.Invoke();
        }
    }

    public partial class ReceiveLineItemViewModel : ObservableObject
    {
        private readonly OrderLine _line;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NewTotalReceived))]
        [NotifyPropertyChangedFor(nameof(NewRemaining))]
        private double _receiveNow;

        public ReceiveLineItemViewModel(OrderLine line)
        {
            _line = line;
            // Default to 0 or remaining? Legacy used 0.
        }

        public OrderLine SourceLine => _line;
        public string Description => _line.Description;
        public string ItemCode => _line.ItemCode;
        public double QuantityOrdered => _line.QuantityOrdered;
        public double QuantityReceivedSoFar => _line.QuantityReceived;
        public double Remaining => Math.Max(0, _line.QuantityOrdered - _line.QuantityReceived);
        public double NewTotalReceived => _line.QuantityReceived + ReceiveNow;
        public double NewRemaining => Math.Max(0, _line.QuantityOrdered - NewTotalReceived);
        public string UnitOfMeasure => _line.UnitOfMeasure;
    }
}
