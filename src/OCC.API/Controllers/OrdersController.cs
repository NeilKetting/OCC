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
    [Authorize(Roles = "Admin,Office")]
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IStockService _stockService;
        private readonly ILogger<OrdersController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public OrdersController(AppDbContext context, ILogger<OrdersController> logger, IHubContext<NotificationHub> hubContext, IStockService stockService)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _stockService = stockService;
        }

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
                        ExpectedDeliveryDate = o.ExpectedDeliveryDate, // Added this field
                        SupplierName = o.SupplierName,
                        ProjectName = o.ProjectName ?? string.Empty,
                        Status = o.Status,
                        TotalAmount = o.Lines.Sum(l => l.LineTotal + l.VatAmount),
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

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> GetOrder(Guid id)
        {
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

        [HttpPost]
        public async Task<ActionResult<OrderDto>> CreateOrder(OrderDto orderDto)
        {
            try
            {
                if (orderDto == null) return BadRequest("Order data is null.");

                // Validate order
                if (!orderDto.Lines.Any())
                    return BadRequest("Order must have at least one line item.");

                if (orderDto.ExpectedDeliveryDate.HasValue && orderDto.ExpectedDeliveryDate.Value.Date < DateTime.Today)
                    return BadRequest("Expected delivery date (ETA) cannot be in the past.");

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

                    if (line.QuantityOrdered <= 0)
                        return BadRequest("All line items must have a quantity greater than zero.");

                    // UnitPrice 0 allowed (e.g. for Picking Orders / Samples)
                    if (line.UnitPrice < 0) 
                         return BadRequest("All line items must have a unit price greater than or equal to zero.");

                    line.Id = Guid.NewGuid();
                    line.OrderId = order.Id;
                }

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

                _logger.LogInformation("Order {OrderNumber} created by {User}", order.OrderNumber, User.FindFirst(ClaimTypes.Name)?.Value);

                var resultDto = ToDto(order);

                // Notify clients via SignalR
                // Note: We send the Full DTO or Summary? 
                // Currently listeners might expect Entity. Let's send DTO. 
                // Clients must be updated to handle this change.
                await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", resultDto);

                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, resultDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating order {OrderNumber}", orderDto?.OrderNumber);
                return StatusCode(500, "An error occurred while creating the order.");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(Guid id, OrderDto orderDto)
        {
            if (id != orderDto.Id)
                return BadRequest();

            try
            {
                // 1. Load existing WITH lines
                var existingOrder = await _context.Orders
                                            .Include(o => o.Lines)
                                            .FirstOrDefaultAsync(o => o.Id == id);
                                            
                if (existingOrder == null) return NotFound();

                // 2. Update scalar properties
                // Map Scalar Properties manually or auto-map
                existingOrder.OrderNumber = orderDto.OrderNumber; // Usually readonly but...
                // existingOrder.OrderDate = orderDto.OrderDate; // Keep original date? Or allow update? 
                // Let's assume Order Date allows edit if it was wrong.
                
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

                if (orderDto.ExpectedDeliveryDate.HasValue && orderDto.ExpectedDeliveryDate.Value.Date < DateTime.Today)
                    return BadRequest("Expected delivery date (ETA) cannot be in the past.");

                // 3. Reconcile Lines (Smart Merge)
                foreach (var lineDto in orderDto.Lines)
                {
                    // Validation
                    if (lineDto.QuantityOrdered <= 0) return BadRequest("Quantity must be > 0");
                    if (lineDto.UnitPrice < 0) return BadRequest("All line items must have a unit price greater than or equal to zero.");

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
                        existingLine.QuantityReceived = lineDto.QuantityReceived; // Allow update?
                        existingLine.LineTotal = lineDto.LineTotal;
                        existingLine.VatAmount = lineDto.VatAmount;
                    }
                    else
                    {
                        // Add new
                        var newLine = new OrderLine
                        {
                            Id = Guid.NewGuid(), // Ignore DTO ID if it was temp
                            OrderId = existingOrder.Id,
                            InventoryItemId = lineDto.InventoryItemId,
                            ItemCode = lineDto.ItemCode,
                            Description = lineDto.Description,
                            Category = string.IsNullOrWhiteSpace(lineDto.Category) ? "General" : lineDto.Category,
                            UnitOfMeasure = lineDto.UnitOfMeasure,
                            UnitPrice = lineDto.UnitPrice,
                            QuantityOrdered = lineDto.QuantityOrdered,
                            QuantityReceived = lineDto.QuantityReceived,
                            LineTotal = lineDto.LineTotal,
                            VatAmount = lineDto.VatAmount
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

                if (linesToRemove.Any())
                {
                    // Handle Stock for Removed Lines
                    if (existingOrder.OrderType == OrderType.PickingOrder || existingOrder.OrderType == OrderType.ReturnToInventory)
                    {
                        double undoMultiplier = existingOrder.OrderType == OrderType.PickingOrder ? 1 : -1;
                        foreach (var l in linesToRemove)
                        {
                            if (l.InventoryItemId.HasValue)
                            {
                                await _stockService.AdjustStockAsync(l.InventoryItemId.Value, l.QuantityOrdered * undoMultiplier, existingOrder.Branch);
                            }
                        }
                    }

                    _context.OrderLines.RemoveRange(linesToRemove);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Order {OrderNumber} updated by {User}", orderDto.OrderNumber, User.FindFirst(ClaimTypes.Name)?.Value);

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

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
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

                _logger.LogInformation("Order {OrderNumber} deleted by {User}", order.OrderNumber, User.FindFirst(ClaimTypes.Name)?.Value);

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
        
        [HttpPost("{id}/receive")]
        public async Task<IActionResult> ReceiveOrder(Guid id, [FromBody] List<OrderLineDto> receivedLines)
        {
            // Keeping legacy signature for now, but returning OrderDto potentially?
            // The client expects Order back.
            
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
                                    inventoryItem.AverageCost = (currentTotalValue + receivedTotalValue) / (decimal)newTotalQty;
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
                
                _logger.LogInformation("Order {OrderNumber} received/updated by {User}", order.OrderNumber, User.FindFirst(ClaimTypes.Name)?.Value);
                
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

        [HttpGet("restock-template")]
        public async Task<ActionResult<OrderDto>> GetRestockTemplate()
        {
            try
            {
                var inventory = await _context.InventoryItems.AsNoTracking().ToListAsync();
                var lowStockItems = inventory.Where(i => i.TrackLowStock && (i.JhbQuantity <= i.JhbReorderPoint || i.CptQuantity <= i.CptReorderPoint)).ToList();

                if (!lowStockItems.Any()) 
                {
                    // Return empty template
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
                
                // Try to find supplier details
                // Note: We don't have Supplier Service here, but we can query suppliers table if it existed, 
                // but Supplier name is stored on Item. 
                // Basic lookup if Supplier Table exists (assumed separate Controller/Context usage)
                // For now, just set the name.
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
                        LineTotal = (decimal)needed * unitPrice,
                        VatAmount = ((decimal)needed * unitPrice) * 0.15m
                    });
                }
                
                // Recalculate total
                orderDto.TotalAmount = orderDto.Lines.Sum(l => l.LineTotal) * 1.15m;
                
                return Ok(orderDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating restock template");
                return StatusCode(500, "Error generating restock template");
            }
        }

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
                            
                            // Remaining logic: Max(0, Ordered - Received)
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

        private OrderDto CreateNewOrderDtoTemplate(OrderType type = OrderType.PurchaseOrder)
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
                OrderNumber = $"{prefix}-{DateTime.Now:yyMM}-{new Random().Next(1000, 9999)}",
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
                TotalAmount = order.TotalAmount, // Use calculated
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
                    UnitPrice = l.UnitPrice,
                    VatAmount = l.VatAmount,
                    LineTotal = l.LineTotal,
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
                        UnitPrice = l.UnitPrice,
                        VatAmount = l.VatAmount,
                        LineTotal = l.LineTotal,
                        Remarks = l.Remarks
                    })
                )
            };
        }

        #endregion
    }
}
