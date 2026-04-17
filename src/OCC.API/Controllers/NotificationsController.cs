using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(AppDbContext context, ILogger<NotificationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Notifications
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications()
        {
            try
            {
                var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out var userId))
                {
                    return Unauthorized("User ID not found in claims.");
                }

                return await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.Timestamp)
                    .Take(50) // Limit to last 50
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Notifications/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Notification>> GetNotification(Guid id)
        {
            var notification = await _context.Notifications.FindAsync(id);

            if (notification == null)
            {
                return NotFound();
            }

            return notification;
        }

        // POST: api/Notifications
        [HttpPost]
        public async Task<ActionResult<Notification>> PostNotification(Notification notification)
        {
            try
            {
                if (notification.Id == Guid.Empty) notification.Id = Guid.NewGuid();
                notification.Timestamp = DateTime.UtcNow;
                
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetNotification", new { id = notification.Id }, notification);
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error creating notification");
                 return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Notifications/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(Guid id)
        {
             try
            {
                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null) return NotFound();

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                return NoContent();
            }
             catch (Exception ex)
            {
                 _logger.LogError(ex, "Error deleting notification {Id}", id);
                 return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Notifications/5/Read
        [HttpPut("{id}/Read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
             try
            {
                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null) return NotFound();

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                return NoContent();
            }
             catch (Exception ex)
            {
                 _logger.LogError(ex, "Error marking notification as read {Id}", id);
                 return StatusCode(500, "Internal server error");
            }
        }
        // GET: api/Notifications/Dismissed
        [HttpGet("Dismissed")]
        public async Task<ActionResult<IEnumerable<Guid>>> GetDismissedIds()
        {
            try
            {
                var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                // Fallback for name claim
                if (string.IsNullOrEmpty(userIdString)) userIdString = User.Identity?.Name;
                
                // If using actual User IDs in DB, we need to resolve it. 
                // However, the Dismissal model links by UserId (Guid). 
                // Let's assume the auth token provides the Guid Claim or we resolve it.
                // Re-using logic from GetNotifications check if possible, or simple name check if purely email based.
                // But NotificationDismissal uses Guid UserId.
                
                // Let's rely on finding the user by email if Claim is missing, or NameIdentifier.
                Guid userId = Guid.Empty;
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim != null && Guid.TryParse(idClaim.Value, out var parsed))
                {
                    userId = parsed;
                }
                else
                {
                    // Attempt to resolve via email
                    var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name;
                    if (!string.IsNullOrEmpty(email))
                    {
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                        if (user != null) userId = user.Id;
                    }
                }

                if (userId == Guid.Empty) return Unauthorized("User ID not resolved.");

                return await _context.NotificationDismissals
                    .Where(d => d.UserId == userId)
                    .Select(d => d.EntityId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dismissed IDs");
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Notifications/Dismiss
        [HttpPost("Dismiss")]
        public async Task<IActionResult> Dismiss([FromBody] NotificationDismissal dismissal)
        {
            try
            {
               var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
               Guid userId = Guid.Empty;

               // Resolve User ID same as above (Consider refracting into helper)
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim != null && Guid.TryParse(idClaim.Value, out var parsed))
                {
                    userId = parsed;
                }
                else
                {
                    var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name;
                    if (!string.IsNullOrEmpty(email))
                    {
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                        if (user != null) userId = user.Id;
                    }
                }

                if (userId == Guid.Empty) return Unauthorized("User ID not resolved.");

                dismissal.Id = Guid.NewGuid();
                dismissal.UserId = userId; // Force secure user ID
                dismissal.DismissedAt = DateTime.UtcNow;

                _context.NotificationDismissals.Add(dismissal);
                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dismissing notification");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
