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
        private static readonly List<string> _registrationLogs = new();
        private static readonly List<string> _pushLogs = new();
        private readonly AppDbContext _context;
        private readonly ILogger<NotificationsController> _logger;
        private readonly Services.INotificationService _notificationService;

        public NotificationsController(AppDbContext context, ILogger<NotificationsController> logger, Services.INotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _notificationService = notificationService;
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
        [HttpPost("register-device")]
        public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationRequest request)
        {
            var logEntry = $"[{DateTime.UtcNow:HH:mm:ss}] Attempt from {request?.Platform ?? "Unknown"}: Token={(request?.Token != null ? request.Token.Substring(0, Math.Min(10, request.Token.Length)) + "..." : "NULL")}";
            
            _logger.LogInformation("[Push] {LogEntry}", logEntry);
            try
            {
                var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out var userId))
                {
                    lock(_registrationLogs) { _registrationLogs.Insert(0, logEntry + " - FAILED: Unauthorized (No Claim)"); if(_registrationLogs.Count > 20) _registrationLogs.RemoveAt(20); }
                    return Unauthorized("User ID not found in claims.");
                }

                if (string.IsNullOrWhiteSpace(request.Token))
                {
                    return BadRequest("Device token is required.");
                }

                await _notificationService.RegisterDeviceAsync(userId, request.Token, request.Platform, request.DeviceName);

                lock(_registrationLogs) { _registrationLogs.Insert(0, logEntry + $" - SUCCESS for User ID: {userId}"); if(_registrationLogs.Count > 20) _registrationLogs.RemoveAt(20); }
                return Ok(new { message = "Device registered successfully." });
            }
            catch (Exception ex)
            {
                lock(_registrationLogs) { _registrationLogs.Insert(0, logEntry + $" - ERROR: {ex.Message}"); if(_registrationLogs.Count > 20) _registrationLogs.RemoveAt(20); }
                _logger.LogError(ex, "Error registering device");
                return StatusCode(500, "Internal server error");
            }
        }

        public static void LogPush(string log)
        {
            lock (_pushLogs)
            {
                _pushLogs.Insert(0, $"[{DateTime.UtcNow:HH:mm:ss}] {log}");
                if (_pushLogs.Count > 50) _pushLogs.RemoveAt(50);
            }
        }

        public static void LogPush(string log)
        {
            lock (_pushLogs)
            {
                _pushLogs.Insert(0, $"[{DateTime.UtcNow:HH:mm:ss}] {log}");
                if (_pushLogs.Count > 50) _pushLogs.RemoveAt(50);
            }
        }

        [AllowAnonymous]
        [HttpGet("debug-status/{email}")]
        public async Task<IActionResult> GetDebugStatus(string email, [FromQuery] string? env = null)
        {
            try
            {

                var dbName = _context.Database.GetDbConnection().Database;
                
                if (email.ToLower() == "list")
                {
                    var allUsers = await _context.Users.Select(u => new { u.Email, u.DisplayName, u.Id }).ToListAsync();
                    return Ok(new { Database = dbName, RegistrationLogs = _registrationLogs, UserCount = allUsers.Count, Users = allUsers });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() || u.Id.ToString() == email);
                if (user == null) return NotFound(new { Database = dbName, message = $"User {email} not found." });

                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.LinkedUserId == user.Id);
                string empLinkNote = "Linked by ID";
                if (employee == null)
                {
                    employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
                    if (employee != null) empLinkNote = "Match found by Email (Lazy-link ready)";
                }

                var subContractor = await _context.SubContractors.FirstOrDefaultAsync(s => s.PortalUserId == user.Id);
                string scLinkNote = "Linked by ID";
                if (subContractor == null)
                {
                    subContractor = await _context.SubContractors.FirstOrDefaultAsync(s => s.Email == user.Email);
                    if (subContractor != null) scLinkNote = "Match found by Email (Lazy-link ready)";
                }

                var devices = await _context.UserDevices.Where(d => d.UserId == user.Id).ToListAsync();

                return Ok(new
                {
                    Database = dbName,
                    RegistrationLogs = _registrationLogs,
                    PushLogs = _pushLogs,
                    UserId = user.Id,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    Role = user.UserRole.ToString(),
                    IsEmployeeLinked = employee != null,
                    EmployeeName = employee != null ? $"{employee.FirstName} {employee.LastName}" : "NOT LINKED",
                    EmployeeLinkMethod = employee != null ? empLinkNote : "N/A",
                    IsSubContractorLinked = subContractor != null,
                    SubContractorName = subContractor?.Name ?? "NOT LINKED",
                    SubContractorLinkMethod = subContractor != null ? scLinkNote : "N/A",
                    DeviceCount = devices.Count,
                    Devices = devices.Select(d => new { 
                        d.Platform, 
                        d.DeviceName, 
                        LastSeen = d.LastSeenUtc,
                        TokenPreview = !string.IsNullOrEmpty(d.DeviceToken) ? d.DeviceToken.Substring(0, Math.Min(10, d.DeviceToken.Length)) + "..." : "EMPTY"
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking debug status for {Email}", email);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class DeviceRegistrationRequest
    {
        public string Token { get; set; } = string.Empty;
        public string Platform { get; set; } = "Unknown";
        public string? DeviceName { get; set; }
    }
}
