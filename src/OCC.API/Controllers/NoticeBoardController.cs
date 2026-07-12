using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NoticeBoardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NoticeBoardController> _logger;

        public NoticeBoardController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<NoticeBoardController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/NoticeBoard
        [HttpGet]
        public async Task<ActionResult<IEnumerable<NoticeBoardItem>>> GetActiveNotices()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var query = _context.NoticeBoardItems
                    .Where(n => n.IsActive)
                    .Where(n => n.ExpiryDate == null || n.ExpiryDate.Value.Date >= today);

                var notices = await query
                    .OrderByDescending(n => n.IsPinned)
                    .ThenByDescending(n => n.CreatedAtUtc)
                    .ToListAsync();

                return Ok(notices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active notices");
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/NoticeBoard
        [HttpPost]
        [Authorize(Roles = "Admin, Office")]
        public async Task<ActionResult<NoticeBoardItem>> CreateNotice(NoticeBoardItem item)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                item.Id = Guid.NewGuid();
                item.CreatedAtUtc = DateTime.UtcNow;
                item.IsActive = true;

                // Grab user's display name from claims if present
                var displayName = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value 
                                  ?? User.Identity?.Name 
                                  ?? "System";
                item.CreatedBy = displayName;

                _context.NoticeBoardItems.Add(item);
                await _context.SaveChangesAsync();

                // Broadcast real-time SignalR entity update
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "NoticeBoardItem", "Create", item.Id.ToString());

                return CreatedAtAction(nameof(GetActiveNotices), new { id = item.Id }, item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notice board item");
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/NoticeBoard/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> DeleteNotice(Guid id)
        {
            try
            {
                var item = await _context.NoticeBoardItems.FindAsync(id);
                if (item == null || !item.IsActive)
                {
                    return NotFound();
                }

                // We can do a soft delete (IsActive = false)
                item.IsActive = false;
                item.UpdatedAtUtc = DateTime.UtcNow;
                item.UpdatedBy = User.Identity?.Name ?? "System";

                await _context.SaveChangesAsync();

                // Broadcast real-time SignalR entity update
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "NoticeBoardItem", "Delete", id.ToString());

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft-deleting notice board item {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
