using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System.Security.Claims;

namespace OCC.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public InventoryController(AppDbContext context, ILogger<InventoryController> logger, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        [HttpGet("summaries")]
        public async Task<ActionResult<IEnumerable<InventorySummaryDto>>> GetInventorySummaries()
        {
            try
                {
                var summaries = await _context.InventoryItems
                    .OrderBy(i => i.Description)
                    .Select(i => new InventorySummaryDto
                    {
                        Id = i.Id,
                        Sku = i.Sku,
                        Description = i.Description,
                        Category = i.Category,
                        JhbQuantity = i.JhbQuantity,
                        CptQuantity = i.CptQuantity,
                        QuantityOnHand = i.QuantityOnHand,
                        Price = i.Price,
                        Location = i.Location,
                        UnitOfMeasure = i.UnitOfMeasure,
                        InventoryStatus = i.InventoryStatus
                    })
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(summaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving inventory summaries");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<InventoryItem>>> GetInventory()
        {
            try
            {
                var items = await _context.InventoryItems
                    .AsNoTracking()
                    .OrderBy(i => i.Description)
                    .ToListAsync();

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query full InventoryItems entity. Attempting fallback projection for schema compatibility.");
                try
                {
                    var fallbackItems = await _context.InventoryItems
                        .AsNoTracking()
                        .OrderBy(i => i.Description)
                        .Select(i => new InventoryItem
                        {
                            Id = i.Id,
                            Sku = i.Sku,
                            Description = i.Description,
                            Supplier = i.Supplier,
                            Category = i.Category,
                            Location = i.Location,
                            JhbQuantity = i.JhbQuantity,
                            CptQuantity = i.CptQuantity,
                            JhbReorderPoint = i.JhbReorderPoint,
                            CptReorderPoint = i.CptReorderPoint,
                            UnitOfMeasure = i.UnitOfMeasure,
                            AverageCost = i.AverageCost,
                            Price = i.Price,
                            TrackLowStock = i.TrackLowStock,
                            Type = i.Type
                        })
                        .ToListAsync();

                    return Ok(fallbackItems);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Error occurred while fetching inventory via fallback.");
                    return StatusCode(500, $"An error occurred while fetching inventory: {ex.Message}");
                }
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryItem>> GetInventoryItem(Guid id)
        {
            try
            {
                var item = await _context.InventoryItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (item == null)
                    return NotFound();

                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query full InventoryItem entity for ID {ItemId}. Attempting fallback projection for schema compatibility.", id);
                try
                {
                    var fallbackItem = await _context.InventoryItems
                        .AsNoTracking()
                        .Where(i => i.Id == id)
                        .Select(i => new InventoryItem
                        {
                            Id = i.Id,
                            Sku = i.Sku,
                            Description = i.Description,
                            Supplier = i.Supplier,
                            Category = i.Category,
                            Location = i.Location,
                            JhbQuantity = i.JhbQuantity,
                            CptQuantity = i.CptQuantity,
                            JhbReorderPoint = i.JhbReorderPoint,
                            CptReorderPoint = i.CptReorderPoint,
                            UnitOfMeasure = i.UnitOfMeasure,
                            AverageCost = i.AverageCost,
                            Price = i.Price,
                            TrackLowStock = i.TrackLowStock,
                            Type = i.Type
                        })
                        .FirstOrDefaultAsync();

                    if (fallbackItem == null)
                        return NotFound();

                    return Ok(fallbackItem);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Error occurred while fetching inventory item {ItemId} via fallback.", id);
                    return StatusCode(500, "An error occurred while fetching the inventory item.");
                }
            }
        }

        [HttpPost]
        public async Task<ActionResult<InventoryItem>> CreateItem(InventoryItem item)
        {
            try
            {
                if (item == null) return BadRequest("Item data is null.");

                if (!string.IsNullOrWhiteSpace(item.Sku))
                {
                    var skuExists = await _context.InventoryItems.AnyAsync(i => i.Sku.ToLower() == item.Sku.ToLower());
                    if (skuExists)
                    {
                        return BadRequest($"An inventory item with SKU '{item.Sku}' already exists.");
                    }
                }

                item.Id = Guid.NewGuid();
                item.QuantityOnHand = item.JhbQuantity + item.CptQuantity;

                _context.InventoryItems.Add(item);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Inventory item {Description} created by {User}", item.Description, User.FindFirst(ClaimTypes.Name)?.Value);

                // Notify clients
                await _hubContext.Clients.All.SendAsync("ReceiveInventoryUpdate", "ItemCreated");

                return CreatedAtAction(nameof(GetInventoryItem), new { id = item.Id }, item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating inventory item {Description}", item?.Description);
                return StatusCode(500, "An error occurred while creating the inventory item.");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateItem(Guid id, InventoryItem item)
        {
            if (id != item.Id)
                return BadRequest();

            if (!string.IsNullOrWhiteSpace(item.Sku))
            {
                var skuExists = await _context.InventoryItems.AnyAsync(i => i.Id != id && i.Sku.ToLower() == item.Sku.ToLower());
                if (skuExists)
                {
                    return BadRequest($"An inventory item with SKU '{item.Sku}' already exists.");
                }
            }

            var existingItem = await _context.InventoryItems.FindAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            item.QuantityOnHand = item.JhbQuantity + item.CptQuantity;
            _context.Entry(existingItem).CurrentValues.SetValues(item);

            try
            {
                await _context.SaveChangesAsync();

                _logger.LogInformation("Inventory item {Description} updated by {User}", item.Description, User.FindFirst(ClaimTypes.Name)?.Value);

                // Notify clients
                await _hubContext.Clients.All.SendAsync("ReceiveInventoryUpdate", "ItemUpdated");

                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryItemExists(id))
                    return NotFound();
                else
                    throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating inventory item {ItemId}", id);
                return StatusCode(500, "An error occurred while updating the inventory item.");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventoryItem(Guid id)
        {
            try
            {
                var item = await _context.InventoryItems.FindAsync(id);
                if (item == null)
                {
                    return NotFound();
                }

                // Check for usage in OrderLines
                bool isUsed = await _context.OrderLines.AnyAsync(ol => ol.InventoryItemId == id);
                if (isUsed)
                {
                    return Conflict("Item cannot be deleted because it is used in existing orders.");
                }

                _context.InventoryItems.Remove(item);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Inventory item {Description} deleted by {User}", item.Description, User.FindFirst(ClaimTypes.Name)?.Value);
                await _hubContext.Clients.All.SendAsync("ReceiveInventoryUpdate", "ItemDeleted");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inventory item {ItemId}", id);
                return StatusCode(500, "An error occurred while deleting the item.");
            }
        }

        private bool InventoryItemExists(Guid id)
        {
            return _context.InventoryItems.Any(e => e.Id == id);
        }
    }
}
