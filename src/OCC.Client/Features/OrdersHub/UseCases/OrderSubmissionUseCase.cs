using Microsoft.Extensions.Logging;
using OCC.Client.ModelWrappers;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.OrdersHub.UseCases
{
    public record OrderSubmissionOptions(
        bool ShouldPrint,
        bool ShouldEmail,
        bool IsNewOrder);

    public class OrderSubmissionUseCase
    {
        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly ILogger<OrderSubmissionUseCase> _logger;
        private readonly IPdfService _pdfService;

        public OrderSubmissionUseCase(
            IOrderManager orderManager,
            IDialogService dialogService,
            ILogger<OrderSubmissionUseCase> logger,
            IPdfService pdfService)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _logger = logger;
            _pdfService = pdfService;
        }

        public virtual async Task<(bool Success, Order? Result)> ExecuteAsync(OrderWrapper orderWrapper, OrderSubmissionOptions options)
        {
            // 1. Validation
            if (!await ValidateAsync(orderWrapper)) return (false, null);

            try
            {
                // 2. Sanitize and Commit
                var orderToSubmit = Sanitize(orderWrapper);

                // 3. Persist
                Order result;
                if (options.IsNewOrder)
                {
                    result = await _orderManager.CreateOrderAsync(orderToSubmit);
                }
                else
                {
                    await _orderManager.UpdateOrderAsync(orderToSubmit);
                    result = orderToSubmit;
                }

                // 4. Post-Submit Actions
                await HandlePostSubmitActionsAsync(result, options);

                return (true, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting order");
                await _dialogService.ShowAlertAsync("Error", $"Failed to submit order: {ex.Message}");
                return (false, null);
            }
        }

        private async Task<bool> ValidateAsync(OrderWrapper order)
        {
            if (order.OrderType == OrderType.PurchaseOrder && !order.SupplierId.HasValue && string.IsNullOrEmpty(order.SupplierName))
            {
                await _dialogService.ShowAlertAsync("Validation Error", "Please select a supplier.");
                return false;
            }

            if (order.ExpectedDeliveryDate == null)
            {
                 await _dialogService.ShowAlertAsync("Validation Error", "Please select an Expected Delivery Date.");
                 return false;
            }

            if (order.ExpectedDeliveryDate.Value.Date < DateTime.Today)
            {
                await _dialogService.ShowAlertAsync("Validation Error", "Expected delivery date cannot be in the past.");
                return false;
            }

            if (order.OrderType == OrderType.PickingOrder && !order.CustomerId.HasValue)
            {
                await _dialogService.ShowAlertAsync("Validation Error", "Please select a customer.");
                return false;
            }

            var meaningfulLines = order.Lines.Where(l => 
                l.InventoryItemId != null || 
                !string.IsNullOrWhiteSpace(l.ItemCode) || 
                !string.IsNullOrWhiteSpace(l.Description) || 
                l.QuantityOrdered > 0 || 
                (order.OrderType != OrderType.PickingOrder && l.UnitPrice > 0))
                .ToList();

            if (!meaningfulLines.Any())
            {
                await _dialogService.ShowAlertAsync("Validation Error", "Please add at least one line item.");
                return false;
            }

            var invalidLines = meaningfulLines.Where(l => 
                l.InventoryItemId == null || 
                string.IsNullOrWhiteSpace(l.Description) || 
                l.QuantityOrdered <= 0 || 
                (order.OrderType != OrderType.PickingOrder && l.UnitPrice < 0) || 
                string.IsNullOrWhiteSpace(l.UnitOfMeasure))
                .ToList();

            if (invalidLines.Any())
            {
                var firstInvalid = invalidLines.First();
                string missing = "";
                if (firstInvalid.InventoryItemId == null) missing = "Inventory Item";
                else if (string.IsNullOrWhiteSpace(firstInvalid.Description)) missing = "Description";
                else if (firstInvalid.QuantityOrdered <= 0) missing = "Quantity";
                else if (firstInvalid.UnitPrice <= 0) missing = "Unit Price";
                else if (string.IsNullOrWhiteSpace(firstInvalid.UnitOfMeasure)) missing = "Unit of Measure";

                await _dialogService.ShowAlertAsync("Validation Error", 
                    $"Some items are incomplete. Every line must have an Inventory Item, Description, Quantity and UOM.\n\nFirst issue found: Missing {missing}.");
                return false;
            }

            return true;
        }

        private Order Sanitize(OrderWrapper orderWrapper)
        {
            orderWrapper.CommitToModel();
            var model = orderWrapper.Model;

            // Filter out empty lines
            var meaningfulLines = model.Lines
                .Where(l => l.InventoryItemId != Guid.Empty || !string.IsNullOrWhiteSpace(l.ItemCode) || !string.IsNullOrWhiteSpace(l.Description))
                .Where(l => l.QuantityOrdered > 0)
                .ToList();

            model.Lines = new ObservableCollection<OrderLine>(meaningfulLines);
            return model;
        }

        private async Task HandlePostSubmitActionsAsync(Order order, OrderSubmissionOptions options)
        {
            // Sync Inventory Item Prices if they have changed
            foreach (var line in order.Lines)
            {
                if (line.InventoryItemId.HasValue && line.UnitPrice > 0)
                {
                    try
                    {
                        var item = await _orderManager.GetItemByIdAsync(line.InventoryItemId.Value);
                        if (item != null && item.Price != line.UnitPrice)
                        {
                            item.Price = line.UnitPrice;
                            // OrderLinesViewModel uses UpdateInventoryItemAsync, while ItemDetailViewModel uses UpdateItemAsync.
                            // However, IOrderManager interface in OrderLines uses UpdateInventoryItemAsync. Let's use UpdateItemAsync 
                            // as we know it exists for sure, but actually let's just stick to UpdateItemAsync as used in ItemDetail.
                            await _orderManager.UpdateItemAsync(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to sync price for item {ItemId}", line.InventoryItemId);
                    }
                }
            }

            if (options.ShouldEmail)
            {
                await _dialogService.ShowAlertAsync("Info", "Email functionality is coming soon.");
            }

            if (options.ShouldPrint)
            {
                try
                {
                    var path = await _pdfService.GenerateOrderPdfAsync(order, isPrintVersion: true);
                    new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true } }.Start();
                }
                catch(Exception ex) 
                { 
                    _logger.LogError(ex, "Failed to print order"); 
                }
            }
        }
    }
}
