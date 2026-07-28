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
    /// <summary>
    /// API Controller for inventory catalog management and stock summaries.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="InventoryController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="hubContext">The SignalR hub context.</param>
        public InventoryController(AppDbContext context, ILogger<InventoryController> logger, IHubContext<NotificationHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        /// <summary>
        /// Retrieves light-weight summaries for all inventory items.
        /// </summary>
        /// <returns>A collection of inventory summary DTOs.</returns>
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
                        Price = Math.Round(i.Price, 2, MidpointRounding.AwayFromZero),
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
                return StatusCode(500, "An error occurred while retrieving inventory summaries.");
            }
        }

        /// <summary>
        /// Retrieves all full inventory items.
        /// </summary>
        /// <returns>A list of inventory item entity objects.</returns>
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
                _logger.LogError(ex, "Error occurred while fetching inventory.");
                return StatusCode(500, "An error occurred while fetching inventory.");
            }
        }

        /// <summary>
        /// Retrieves a specific inventory item by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the inventory item.</param>
        /// <returns>The inventory item if found; otherwise, 404 Not Found.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryItem>> GetInventoryItem(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid item ID.");

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
                _logger.LogError(ex, "Error occurred while fetching inventory item {ItemId}", id);
                return StatusCode(500, "An error occurred while fetching the inventory item.");
            }
        }

        /// <summary>
        /// Creates a new inventory item.
        /// </summary>
        /// <param name="item">The inventory item payload.</param>
        /// <returns>The created inventory item.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin,Office")]
        public async Task<ActionResult<InventoryItem>> CreateItem([FromBody] InventoryItem item)
        {
            if (item == null) return BadRequest("Item data is null.");

            if (string.IsNullOrWhiteSpace(item.Description))
                return BadRequest("Inventory item description is required.");

            if (item.Price < 0)
                return BadRequest("Inventory item price cannot be negative.");

            if (item.AverageCost < 0)
                return BadRequest("Inventory item average cost cannot be negative.");

            try
            {
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
                item.Price = Math.Round(item.Price, 2, MidpointRounding.AwayFromZero);
                item.AverageCost = Math.Round(item.AverageCost, 2, MidpointRounding.AwayFromZero);

                _context.InventoryItems.Add(item);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Inventory item {Description} created by {User}", item.Description, User?.FindFirst(ClaimTypes.Name)?.Value);

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

        /// <summary>
        /// Updates an existing inventory item.
        /// </summary>
        /// <param name="id">The unique identifier of the item to update.</param>
        /// <param name="item">The updated inventory item payload.</param>
        /// <returns>No content on success; bad request or not found on failure.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> UpdateItem(Guid id, [FromBody] InventoryItem item)
        {
            if (item == null || id != item.Id)
                return BadRequest("Item ID mismatch or payload is null.");

            if (string.IsNullOrWhiteSpace(item.Description))
                return BadRequest("Inventory item description is required.");

            if (item.Price < 0)
                return BadRequest("Inventory item price cannot be negative.");

            if (item.AverageCost < 0)
                return BadRequest("Inventory item average cost cannot be negative.");

            try
            {
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
                item.Price = Math.Round(item.Price, 2, MidpointRounding.AwayFromZero);
                item.AverageCost = Math.Round(item.AverageCost, 2, MidpointRounding.AwayFromZero);

                _context.Entry(existingItem).CurrentValues.SetValues(item);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Inventory item {Description} updated by {User}", item.Description, User?.FindFirst(ClaimTypes.Name)?.Value);

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

        /// <summary>
        /// Deletes an inventory item by ID if not used in active orders.
        /// </summary>
        /// <param name="id">The unique identifier of the inventory item to delete.</param>
        /// <returns>No content on success; conflict if item is referenced in orders.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> DeleteInventoryItem(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid item ID.");

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

                _logger.LogInformation("Inventory item {Description} deleted by {User}", item.Description, User?.FindFirst(ClaimTypes.Name)?.Value);
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
