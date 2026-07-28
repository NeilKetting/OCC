using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System.Security.Claims;
using OCC.API.Services;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for managing operations, purchase orders, picking orders, returns, and inventory receiving.
    /// </summary>
    [Authorize(Roles = "Admin,Office")]
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IStockService _stockService;
        private readonly ILogger<OrdersController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrdersController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="hubContext">The SignalR hub context for real-time notifications.</param>
        /// <param name="stockService">The stock service for inventory adjustments.</param>
        public OrdersController(AppDbContext context, ILogger<OrdersController> logger, IHubContext<NotificationHub> hubContext, IStockService stockService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
        }

        /// <summary>
        /// Retrieves a summary list of all orders.
        /// </summary>
        /// <returns>A list of order summary DTOs.</returns>
        [HttpGet]
        public async Task<ActionResult<List<OrderSummaryDto>>> GetOrders()
        {
            try
            {
                var orders = await _context.Orders
                    .AsNoTracking()
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new OrderSummaryDto
                    {
                        Id = o.Id,
                        OrderNumber = o.OrderNumber,
                        OrderDate = o.OrderDate,
                        ExpectedDeliveryDate = o.ExpectedDeliveryDate,
                        SupplierName = o.SupplierName,
                        ProjectName = o.ProjectName ?? string.Empty,
                        Status = o.Status,
                        TotalAmount = Math.Round(o.Lines.Sum(l => l.LineTotal + l.VatAmount), 2, MidpointRounding.AwayFromZero),
                        Branch = o.Branch.ToString(),
                        SupplierId = o.SupplierId,
                        OrderType = o.OrderType,
                        DestinationDisplay = o.DestinationType == OrderDestinationType.Site ? $"Site: {o.ProjectName}" : "Office Stock"
                    })
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching orders.");
                return StatusCode(500, "An error occurred while fetching orders.");
            }
        }

        /// <summary>
        /// Retrieves detailed information for a specific order by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the order.</param>
        /// <returns>The order DTO if found; otherwise, 404 Not Found.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> GetOrder(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid order ID.");

            try
            {
                var order = await _context.Orders
                    .Include(o => o.Lines)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                    return NotFound();

                return Ok(ToDto(order));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching order {OrderId}", id);
                return StatusCode(500, "An error occurred while fetching the order.");
            }
        }

        /// <summary>
        /// Creates a new purchase, picking, or return order.
        /// </summary>
        /// <param name="orderDto">The order payload data.</param>
        /// <returns>The created order DTO.</returns>
        [HttpPost]
        public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] OrderDto orderDto)
        {
            if (orderDto == null) return BadRequest("Order data is null.");

            try
            {
                // Validate order
                if (orderDto.Lines == null || !orderDto.Lines.Any())
                    return BadRequest("Order must have at least one line item.");

                if (orderDto.TaxRate < 0)
                    return BadRequest("Tax rate cannot be negative.");

                if (orderDto.ExpectedDeliveryDate.HasValue && orderDto.ExpectedDeliveryDate.Value.Date < DateTime.Today)
                    return BadRequest("Expected delivery date (ETA) cannot be in the past.");

                // Check for duplicate order number if specified
                if (!string.IsNullOrWhiteSpace(orderDto.OrderNumber))
                {
                    var exists = await _context.Orders.AnyAsync(o => o.OrderNumber == orderDto.OrderNumber);
                    if (exists)
                        return BadRequest($"Order number '{orderDto.OrderNumber}' is already in use.");
                }

                var order = ToEntity(orderDto);

                // Set server-side properties (safety)
                order.Id = Guid.NewGuid();
                order.OrderDate = DateTime.UtcNow; // Enforce UTC
                
                // Ensure lines are valid and have IDs
                foreach (var line in order.Lines)
                {
                    if (line.InventoryItemId == null || line.InventoryItemId == Guid.Empty)
                        return BadRequest("All line items must be linked to an Inventory Item.");

                    if (string.IsNullOrWhiteSpace(line.Description))
                        return BadRequest("All line items must have a description.");

                    if (line.QuantityOrdered < 0)
                        return BadRequest("Quantity ordered cannot be negative.");

                    if (line.UnitPrice < 0) 
                         return BadRequest("All line items must have a unit price greater than or equal to zero.");

                    line.Id = Guid.NewGuid();
                    line.OrderId = order.Id;
                    line.LineTotal = Math.Round((decimal)line.QuantityOrdered * line.UnitPrice, 2, MidpointRounding.AwayFromZero);
                    line.VatAmount = Math.Round(line.LineTotal * order.TaxRate, 2, MidpointRounding.AwayFromZero);
                }

                order.TotalAmount = Math.Round(order.Lines.Sum(l => l.LineTotal + l.VatAmount), 2, MidpointRounding.AwayFromZero);

                _context.Orders.Add(order);

                // Stock Adjustment
                if (order.OrderType == OrderType.PickingOrder)
                {
                    foreach (var line in order.Lines)
                    {
                        if (line.InventoryItemId.HasValue)
                        {
                            await _stockService.AdjustStockAsync(line.InventoryItemId.Value, -line.QuantityOrdered, order.Branch);
                        }
                    }
                }
                else if (order.OrderType == OrderType.ReturnToInventory)
                {
                    foreach (var line in order.Lines)
                    {
                        if (line.InventoryItemId.HasValue)
                        {
                            await _stockService.AdjustStockAsync(line.InventoryItemId.Value, line.QuantityOrdered, order.Branch);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Order {OrderNumber} created by {User}", order.OrderNumber, User?.FindFirst(ClaimTypes.Name)?.Value ?? "System");

                var resultDto = ToDto(order);

                // Notify clients via SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", resultDto);

                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, resultDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating order {OrderNumber}", orderDto?.OrderNumber);
                return StatusCode(500, "An error occurred while creating the order.");
            }
        }

        /// <summary>
        /// Updates an existing order.
        /// </summary>
        /// <param name="id">The unique identifier of the order to update.</param>
        /// <param name="orderDto">The updated order payload data.</param>
        /// <returns>No content on success; bad request or not found on validation failure.</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(Guid id, [FromBody] OrderDto orderDto)
        {
            if (orderDto == null || id != orderDto.Id)
                return BadRequest("Order ID mismatch or order payload is null.");

            try
            {
                if (orderDto.TaxRate < 0)
                    return BadRequest("Tax rate cannot be negative.");

                if (orderDto.ExpectedDeliveryDate.HasValue && orderDto.ExpectedDeliveryDate.Value.Date < DateTime.Today)
                    return BadRequest("Expected delivery date (ETA) cannot be in the past.");

                // Check for duplicate order number
                var exists = await _context.Orders.AnyAsync(o => o.OrderNumber == orderDto.OrderNumber && o.Id != id);
                if (exists)
                    return BadRequest($"Order number '{orderDto.OrderNumber}' is already in use.");

                // 1. Load existing WITH lines
                var existingOrder = await _context.Orders
                                            .Include(o => o.Lines)
                                            .FirstOrDefaultAsync(o => o.Id == id);
                                            
                if (existingOrder == null) return NotFound();

                // 2. Update scalar properties
                existingOrder.OrderNumber = orderDto.OrderNumber;
                existingOrder.ExpectedDeliveryDate = orderDto.ExpectedDeliveryDate;
                existingOrder.OrderType = orderDto.OrderType;
                existingOrder.Branch = orderDto.Branch;
                existingOrder.SupplierId = orderDto.SupplierId;
                existingOrder.SupplierName = orderDto.SupplierName;
                existingOrder.CustomerId = orderDto.CustomerId;
                existingOrder.EntityAddress = orderDto.EntityAddress;
                existingOrder.EntityTel = orderDto.EntityTel;
                existingOrder.EntityVatNo = orderDto.EntityVatNo;
                existingOrder.DestinationType = orderDto.DestinationType;
                existingOrder.ProjectId = orderDto.ProjectId;
                existingOrder.ProjectName = orderDto.ProjectName;
                existingOrder.Attention = orderDto.Attention;
                existingOrder.TaxRate = orderDto.TaxRate;
                existingOrder.Status = orderDto.Status;
                existingOrder.Notes = orderDto.Notes;
                existingOrder.DeliveryInstructions = orderDto.DeliveryInstructions;
                existingOrder.ScopeOfWork = orderDto.ScopeOfWork;

                // 3. Reconcile Lines (Smart Merge)
                foreach (var lineDto in orderDto.Lines)
                {
                    // Validation
                    if (lineDto.QuantityOrdered < 0) return BadRequest("Quantity ordered cannot be negative.");
                    if (lineDto.UnitPrice < 0) return BadRequest("All line items must have a unit price greater than or equal to zero.");

                    decimal computedLineTotal = Math.Round((decimal)lineDto.QuantityOrdered * lineDto.UnitPrice, 2, MidpointRounding.AwayFromZero);
                    decimal computedVatAmount = Math.Round(computedLineTotal * existingOrder.TaxRate, 2, MidpointRounding.AwayFromZero);

                    var existingLine = existingOrder.Lines.FirstOrDefault(l => l.Id == lineDto.Id);
                    if (existingLine != null)
                    {
                        // Handle Stock reconciliation for modifications (Picking/Return)
                        if (existingOrder.OrderType == OrderType.PickingOrder || existingOrder.OrderType == OrderType.ReturnToInventory)
                        {
                            double multiplier = existingOrder.OrderType == OrderType.PickingOrder ? -1 : 1;
                            var diff = lineDto.QuantityOrdered - existingLine.QuantityOrdered;
                            if (diff != 0 && existingLine.InventoryItemId.HasValue)
                            {
                                await _stockService.AdjustStockAsync(existingLine.InventoryItemId.Value, diff * multiplier, existingOrder.Branch);
                            }
                        }

                        // Update existing
                        existingLine.InventoryItemId = lineDto.InventoryItemId;
                        existingLine.ItemCode = lineDto.ItemCode;
                        existingLine.Description = lineDto.Description;
                        existingLine.Category = lineDto.Category;
                        existingLine.UnitOfMeasure = lineDto.UnitOfMeasure;
                        existingLine.UnitPrice = lineDto.UnitPrice;
                        existingLine.QuantityOrdered = lineDto.QuantityOrdered;
                        existingLine.QuantityReceived = lineDto.QuantityReceived;
                        existingLine.LineTotal = computedLineTotal;
                        existingLine.VatAmount = computedVatAmount;
                    }
                    else
                    {
                        // Add new
                        var newLine = new OrderLine
                        {
                            Id = lineDto.Id != Guid.Empty ? lineDto.Id : Guid.NewGuid(),
                            OrderId = existingOrder.Id,
                            InventoryItemId = lineDto.InventoryItemId,
                            ItemCode = lineDto.ItemCode,
                            Description = lineDto.Description,
                            Category = string.IsNullOrWhiteSpace(lineDto.Category) ? "General" : lineDto.Category,
                            UnitOfMeasure = lineDto.UnitOfMeasure,
                            UnitPrice = lineDto.UnitPrice,
                            QuantityOrdered = lineDto.QuantityOrdered,
                            QuantityReceived = lineDto.QuantityReceived,
                            LineTotal = computedLineTotal,
                            VatAmount = computedVatAmount,
                            Remarks = lineDto.Remarks
                        };

                        // Handle Stock for New Lines
                        if (existingOrder.OrderType == OrderType.PickingOrder || existingOrder.OrderType == OrderType.ReturnToInventory)
                        {
                            double multiplier = existingOrder.OrderType == OrderType.PickingOrder ? -1 : 1;
                            if (newLine.InventoryItemId.HasValue)
                            {
                                await _stockService.AdjustStockAsync(newLine.InventoryItemId.Value, newLine.QuantityOrdered * multiplier, existingOrder.Branch);
                            }
                        }

                        _context.OrderLines.Add(newLine);
                    }
                }

                // Remove deleted lines
                var linesToRemove = existingOrder.Lines
                    .Where(l => !orderDto.Lines.Any(ol => ol.Id == l.Id))
                    .ToList();

                foreach (var lineToRemove in linesToRemove)
                {
                    // Handle Stock for Removed Lines
                    if (existingOrder.OrderType == OrderType.PickingOrder || existingOrder.OrderType == OrderType.ReturnToInventory)
                    {
                        double undoMultiplier = existingOrder.OrderType == OrderType.PickingOrder ? 1 : -1;
                        if (lineToRemove.InventoryItemId.HasValue)
                        {
                            await _stockService.AdjustStockAsync(lineToRemove.InventoryItemId.Value, lineToRemove.QuantityOrdered * undoMultiplier, existingOrder.Branch);
                        }
                    }

                    var entry = _context.Entry(lineToRemove);
                    if (entry.State == EntityState.Added)
                    {
                        entry.State = EntityState.Detached;
                    }
                    else
                    {
                        _context.OrderLines.Remove(lineToRemove);
                    }
                }

                existingOrder.TotalAmount = Math.Round(existingOrder.Lines.Sum(l => l.LineTotal + l.VatAmount), 2, MidpointRounding.AwayFromZero);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Order {OrderNumber} updated by {User}", orderDto.OrderNumber, User?.FindFirst(ClaimTypes.Name)?.Value ?? "System");

                // Notify clients
                await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", ToDto(existingOrder));

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating order {OrderId}", id);
                return StatusCode(500, "An error occurred while updating the order.");
            }
        }

        /// <summary>
        /// Deletes an order by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the order to delete.</param>
        /// <returns>No content on success; 404 Not Found if order is missing.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid order ID.");

            try
            {
                var order = await _context.Orders
                    .Include(o => o.Lines)
                    .FirstOrDefaultAsync(o => o.Id == id);
                                    
                if (order == null)
                    return NotFound();

                // Handle Stock for Deleted Order (Undo deductions/additions)
                if (order.OrderType == OrderType.PickingOrder || order.OrderType == OrderType.ReturnToInventory)
                {
                    double undoMultiplier = order.OrderType == OrderType.PickingOrder ? 1 : -1;
                    foreach (var l in order.Lines)
                    {
                        if (l.InventoryItemId.HasValue)
                        {
                            await _stockService.AdjustStockAsync(l.InventoryItemId.Value, l.QuantityOrdered * undoMultiplier, order.Branch);
                        }
                    }
                }

                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Order {OrderNumber} deleted by {User}", order.OrderNumber, User?.FindFirst(ClaimTypes.Name)?.Value);

                // Notify clients
                await _hubContext.Clients.All.SendAsync("ReceiveOrderDelete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting order {OrderId}", id);
                return StatusCode(500, "An error occurred while deleting the order.");
            }
        }
        
        /// <summary>
        /// Processes received quantities for order line items and updates inventory stock & weighted average cost.
        /// </summary>
        /// <param name="id">The unique identifier of the order.</param>
        /// <param name="receivedLines">The list of received order line DTOs with quantity received updates.</param>
        /// <returns>The updated order DTO.</returns>
        [HttpPost("{id}/receive")]
        public async Task<IActionResult> ReceiveOrder(Guid id, [FromBody] List<OrderLineDto> receivedLines)
        {
            if (id == Guid.Empty) return BadRequest("Invalid order ID.");
            if (receivedLines == null || !receivedLines.Any())
                return BadRequest("No lines to receive.");

            try
            {
                var order = await _context.Orders
                    .Include(o => o.Lines)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null) return NotFound("Order not found.");

                bool isInbound = order.OrderType == OrderType.PurchaseOrder || order.OrderType == OrderType.ReturnToInventory;

                foreach (var receivedLine in receivedLines)
                {
                    if (receivedLine.QuantityReceived < 0)
                        return BadRequest("Received quantity cannot be negative.");

                    var originalLine = order.Lines.FirstOrDefault(l => l.Id == receivedLine.Id);
                    if (originalLine == null) continue;

                    double delta = receivedLine.QuantityReceived - originalLine.QuantityReceived;
                    
                    if (delta == 0) continue;

                    // Update Order Line
                    originalLine.QuantityReceived = receivedLine.QuantityReceived;

                    // Update Inventory (if Inbound)
                    if (isInbound && originalLine.InventoryItemId.HasValue)
                    {
                        var inventoryItem = await _context.InventoryItems.FindAsync(originalLine.InventoryItemId.Value);
                        if (inventoryItem != null)
                        {
                            if (delta > 0)
                            {
                                decimal currentTotalValue = (decimal)(inventoryItem.QuantityOnHand > 0 ? inventoryItem.QuantityOnHand : 0) * inventoryItem.AverageCost;
                                decimal receivedTotalValue = (decimal)delta * originalLine.UnitPrice;
                                double newTotalQty = (inventoryItem.QuantityOnHand > 0 ? inventoryItem.QuantityOnHand : 0) + delta;

                                if (newTotalQty > 0)
                                {
                                    inventoryItem.AverageCost = Math.Round((currentTotalValue + receivedTotalValue) / (decimal)newTotalQty, 2, MidpointRounding.AwayFromZero);
                                }
                                else if (inventoryItem.QuantityOnHand <= 0) 
                                {
                                     inventoryItem.AverageCost = originalLine.UnitPrice;
                                }
                            }
                                
                            // Update Branch-Specific Quantity
                            if (order.Branch == Branch.JHB) inventoryItem.JhbQuantity += delta;
                            else if (order.Branch == Branch.CPT) inventoryItem.CptQuantity += delta;

                            // Update Total Stock Quantity
                            inventoryItem.QuantityOnHand += delta;
                        }
                    }
                }

                bool allComplete = order.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered);
                bool anyReceived = order.Lines.Any(l => l.QuantityReceived > 0);

                if (allComplete) order.Status = OrderStatus.Completed;
                else if (anyReceived) order.Status = OrderStatus.PartialDelivery;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Order {OrderNumber} received/updated by {User}", order.OrderNumber, User?.FindFirst(ClaimTypes.Name)?.Value);
                
                var dto = ToDto(order);
                await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", dto);
                if (isInbound) await _hubContext.Clients.All.SendAsync("ReceiveInventoryUpdate", "StockReceived");

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing receiving for order {OrderId}", id);
                return StatusCode(500, "An error occurred while receiving the order.");
            }
        }

        /// <summary>
        /// Generates an order template prepopulated with low-stock inventory items for restock replenishment.
        /// </summary>
        /// <returns>A pre-populated order DTO template.</returns>
        [HttpGet("restock-template")]
        public async Task<ActionResult<OrderDto>> GetRestockTemplate()
        {
            try
            {
                var inventory = await _context.InventoryItems.AsNoTracking().ToListAsync();
                var lowStockItems = inventory.Where(i => i.TrackLowStock && (i.JhbQuantity <= i.JhbReorderPoint || i.CptQuantity <= i.CptReorderPoint)).ToList();

                if (!lowStockItems.Any()) 
                {
                    return Ok(CreateNewOrderDtoTemplate());
                }

                // Group by Supplier and pick the one with most items
                var supplierGroups = lowStockItems.GroupBy(i => i.Supplier).OrderByDescending(g => g.Count());
                var topSupplierGroup = supplierGroups.First();
                var supplierName = topSupplierGroup.Key;

                var itemsToOrder = topSupplierGroup.ToList();

                var orderDto = CreateNewOrderDtoTemplate();
                orderDto.ExpectedDeliveryDate = DateTime.Today.AddDays(7);
                orderDto.Notes = $"Auto-generated restock order for {supplierName}.";
                orderDto.SupplierName = supplierName;

                foreach (var item in itemsToOrder)
                {
                    double target = item.JhbReorderPoint * 2; 
                    double needed = target - item.JhbQuantity;
                    
                    if (needed <= 0)
                    {
                        target = item.CptReorderPoint * 2;
                        needed = target - item.CptQuantity;
                    }

                    if (needed < 1) needed = 1;

                    // Find last price efficiently
                    decimal unitPrice = item.AverageCost;
                    var lastLine = await _context.OrderLines
                        .Include(l => l.Order)
                        .Where(l => l.InventoryItemId == item.Id && l.Order.OrderType == OrderType.PurchaseOrder)
                        .OrderByDescending(l => l.Order.OrderDate)
                        .FirstOrDefaultAsync();

                    if (lastLine != null && lastLine.UnitPrice > 0)
                    {
                        unitPrice = lastLine.UnitPrice;
                    }

                    decimal lineTotal = Math.Round((decimal)needed * unitPrice, 2, MidpointRounding.AwayFromZero);
                    decimal vatAmount = Math.Round(lineTotal * 0.15m, 2, MidpointRounding.AwayFromZero);

                    orderDto.Lines.Add(new OrderLineDto
                    {
                        Id = Guid.NewGuid(),
                        InventoryItemId = item.Id,
                        ItemCode = item.Sku,
                        Description = item.Description,
                        Category = item.Category,
                        UnitOfMeasure = item.UnitOfMeasure,
                        UnitPrice = unitPrice,
                        QuantityOrdered = needed,
                        LineTotal = lineTotal,
                        VatAmount = vatAmount
                    });
                }
                
                // Recalculate total
                orderDto.TotalAmount = Math.Round(orderDto.Lines.Sum(l => l.LineTotal + l.VatAmount), 2, MidpointRounding.AwayFromZero);
                
                return Ok(orderDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating restock template");
                return StatusCode(500, "Error generating restock template");
            }
        }

        /// <summary>
        /// Computes restock candidate inventory items filtered by target branch.
        /// </summary>
        /// <param name="branch">Optional branch filter (JHB or CPT).</param>
        /// <returns>A collection of restock candidate DTOs.</returns>
        [HttpGet("restock-candidates")]
        public async Task<ActionResult<IEnumerable<RestockCandidateDto>>> GetRestockCandidates([FromQuery] Branch? branch = null)
        {
            try
            {
                var inventory = await _context.InventoryItems.AsNoTracking().ToListAsync();
                
                // Filter for active POs
                var activePOs = await _context.Orders
                    .Include(o => o.Lines)
                    .Where(o => o.OrderType == OrderType.PurchaseOrder && 
                               (o.Status == OrderStatus.Ordered || o.Status == OrderStatus.PartialDelivery))
                    .ToListAsync();

                // Flatten lines to map: (InventoryId, Branch) -> QuantityRemaining
                var pendingQuantities = new Dictionary<(Guid, Branch), double>();
                
                foreach (var order in activePOs)
                {
                    foreach (var line in order.Lines)
                    {
                        if (line.InventoryItemId.HasValue)
                        {
                            var key = (line.InventoryItemId.Value, order.Branch);
                            if (!pendingQuantities.ContainsKey(key))
                                pendingQuantities[key] = 0;
                            
                            double remaining = Math.Max(0, line.QuantityOrdered - line.QuantityReceived);
                            pendingQuantities[key] += remaining;
                        }
                    }
                }

                var candidates = new List<RestockCandidateDto>();
                
                foreach (var item in inventory)
                {
                    if (!item.TrackLowStock) continue;

                    // Evaluate JHB
                    if (!branch.HasValue || branch == Branch.JHB)
                    {
                        if (item.JhbQuantity <= item.JhbReorderPoint)
                        {
                            double onOrder = 0;
                            pendingQuantities.TryGetValue((item.Id, Branch.JHB), out onOrder);
                            
                            candidates.Add(new RestockCandidateDto
                            {
                                Item = item,
                                QuantityOnOrder = onOrder,
                                TargetBranch = Branch.JHB
                            });
                        }
                    }

                    // Evaluate CPT
                    if (!branch.HasValue || branch == Branch.CPT)
                    {
                        if (item.CptQuantity <= item.CptReorderPoint)
                        {
                            double onOrder = 0;
                            pendingQuantities.TryGetValue((item.Id, Branch.CPT), out onOrder);
                            
                            candidates.Add(new RestockCandidateDto
                            {
                                Item = item,
                                QuantityOnOrder = onOrder,
                                TargetBranch = Branch.CPT
                            });
                        }
                    }
                }

                return Ok(candidates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating restock candidates");
                return StatusCode(500, "Error calculating restock candidates");
            }
        }

        private static OrderDto CreateNewOrderDtoTemplate(OrderType type = OrderType.PurchaseOrder)
        {
            string prefix = type switch
            {
                OrderType.PurchaseOrder => "PO",
                OrderType.PickingOrder => "PK",
                OrderType.ReturnToInventory => "RET",
                _ => "ORD"
            };

            return new OrderDto
            {
                Id = Guid.NewGuid(),
                OrderDate = DateTime.Now,
                OrderNumber = $"{prefix}-{DateTime.Now:yyMM}-{Random.Shared.Next(1000, 9999)}",
                OrderType = type,
                TaxRate = 0.15m,
                DestinationType = OrderDestinationType.Stock,
                Attention = string.Empty,
                Status = OrderStatus.Draft
            };
        }

        #region Mappers

        private static OrderDto ToDto(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                ExpectedDeliveryDate = order.ExpectedDeliveryDate,
                OrderType = order.OrderType,
                Branch = order.Branch,
                SupplierId = order.SupplierId,
                SupplierName = order.SupplierName,
                CustomerId = order.CustomerId,
                EntityAddress = order.EntityAddress,
                EntityTel = order.EntityTel,
                EntityVatNo = order.EntityVatNo,
                DestinationType = order.DestinationType,
                ProjectId = order.ProjectId,
                ProjectName = order.ProjectName,
                Attention = order.Attention,
                TaxRate = order.TaxRate,
                Status = order.Status,
                Notes = order.Notes,
                DeliveryInstructions = order.DeliveryInstructions,
                ScopeOfWork = order.ScopeOfWork,
                Template = order.Template,
                Terms = order.Terms,
                ReferenceNo = order.ReferenceNo,
                TotalAmount = Math.Round(order.TotalAmount, 2, MidpointRounding.AwayFromZero),
                Lines = order.Lines.Select(l => new OrderLineDto
                {
                    Id = l.Id,
                    InventoryItemId = l.InventoryItemId,
                    ItemCode = l.ItemCode,
                    Description = l.Description,
                    Category = l.Category,
                    QuantityOrdered = l.QuantityOrdered,
                    QuantityReceived = l.QuantityReceived,
                    UnitOfMeasure = l.UnitOfMeasure,
                    UnitPrice = Math.Round(l.UnitPrice, 2, MidpointRounding.AwayFromZero),
                    VatAmount = Math.Round(l.VatAmount, 2, MidpointRounding.AwayFromZero),
                    LineTotal = Math.Round(l.LineTotal, 2, MidpointRounding.AwayFromZero),
                    Remarks = l.Remarks
                }).ToList()
            };
        }

        private static Order ToEntity(OrderDto dto)
        {
            return new Order
            {
                Id = dto.Id,
                OrderNumber = dto.OrderNumber,
                OrderDate = dto.OrderDate,
                ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
                OrderType = dto.OrderType,
                Branch = dto.Branch,
                SupplierId = dto.SupplierId,
                SupplierName = dto.SupplierName,
                CustomerId = dto.CustomerId,
                EntityAddress = dto.EntityAddress,
                EntityTel = dto.EntityTel,
                EntityVatNo = dto.EntityVatNo,
                DestinationType = dto.DestinationType,
                ProjectId = dto.ProjectId,
                ProjectName = dto.ProjectName,
                Attention = dto.Attention,
                TaxRate = dto.TaxRate,
                Status = dto.Status,
                Notes = dto.Notes,
                DeliveryInstructions = dto.DeliveryInstructions,
                ScopeOfWork = dto.ScopeOfWork,
                Template = dto.Template,
                Terms = dto.Terms,
                ReferenceNo = dto.ReferenceNo,
                Lines = new System.Collections.ObjectModel.ObservableCollection<OrderLine>(
                    dto.Lines.Select(l => new OrderLine
                    {
                        Id = l.Id,
                        OrderId = dto.Id,
                        InventoryItemId = l.InventoryItemId,
                        ItemCode = l.ItemCode,
                        Description = l.Description,
                        Category = l.Category,
                        QuantityOrdered = l.QuantityOrdered,
                        QuantityReceived = l.QuantityReceived,
                        UnitOfMeasure = l.UnitOfMeasure,
                        UnitPrice = Math.Round(l.UnitPrice, 2, MidpointRounding.AwayFromZero),
                        VatAmount = Math.Round(l.VatAmount, 2, MidpointRounding.AwayFromZero),
                        LineTotal = Math.Round(l.LineTotal, 2, MidpointRounding.AwayFromZero),
                        Remarks = l.Remarks
                    })
                )
            };
        }

        #endregion
    }
}
