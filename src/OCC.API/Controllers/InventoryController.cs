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
                _logger.LogError(ex, "Error occurred while fetching inventory.");
                return StatusCode(500, $"An error occurred while fetching inventory: {ex.Message} {ex.InnerException?.Message}");
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
                _logger.LogError(ex, "Error occurred while fetching inventory item {ItemId}", id);
                return StatusCode(500, "An error occurred while fetching the inventory item.");
            }
        }

        [HttpPost]
        public async Task<ActionResult<InventoryItem>> CreateItem(InventoryItem item)
        {
            try
            {
                if (item == null) return BadRequest("Item data is null.");

                item.Id = Guid.NewGuid();

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

            try
            {
                _context.Entry(item).State = EntityState.Modified;
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
