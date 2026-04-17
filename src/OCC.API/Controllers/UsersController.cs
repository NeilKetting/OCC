using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using OCC.API.Services;
using OCC.API.Hubs;
using System.Security.Cryptography;

namespace OCC.API.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher _passwordHasher;
        private readonly ILogger<UsersController> _logger;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<NotificationHub> _hubContext;

        public UsersController(AppDbContext context, PasswordHasher passwordHasher, ILogger<UsersController> logger, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
            _hubContext = hubContext;
        }

        // GET: api/Users
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            try
            {
                return await _context.Users.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(Guid id)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("Admin");

            if (currentUserId != null && id.ToString() != currentUserId && !isAdmin)
            {
                return Forbid();
            }

            try
            {
                var user = await _context.Users.FindAsync(id);

                if (user == null)
                {
                    return NotFound();
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Users/5/public-key
        [HttpGet("{id}/public-key")]
        public async Task<ActionResult<string>> GetUserPublicKey(Guid id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            if (string.IsNullOrEmpty(user.PublicKey))
            {
                await GenerateProvisionalKeysAsync(user);
            }

            return Ok(new { PublicKey = user.PublicKey });
        }

        [HttpGet("me/provisional-key")]
        public async Task<ActionResult<string>> GetMyProvisionalKey()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out var userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound();

                return Ok(new { ProvisionalPrivateKey = user.ProvisionalPrivateKey });
            }
            return Unauthorized();
        }

        // GET: api/Users/contacts
        [HttpGet("contacts")]
        public async Task<ActionResult<IEnumerable<OCC.Shared.DTOs.ChatUserDto>>> GetContacts()
        {
            var currentUserIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            try
            {
                var users = await _context.Users
                    .Where(u => u.Id.ToString() != currentUserIdStr) // Exclude self
                    .ToListAsync();

                var contacts = new List<OCC.Shared.DTOs.ChatUserDto>();
                bool anyGenerated = false;

                foreach (var u in users)
                {
                    if (string.IsNullOrEmpty(u.PublicKey))
                    {
                        await GenerateProvisionalKeysAsync(u);
                        anyGenerated = true;
                    }

                    contacts.Add(new OCC.Shared.DTOs.ChatUserDto
                    {
                        UserId = u.Id,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        PublicKey = u.PublicKey
                    });
                }

                if (anyGenerated)
                {
                    await _context.SaveChangesAsync();
                }
                    
                return Ok(contacts.OrderBy(u => u.FirstName).ThenBy(u => u.LastName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contacts");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task GenerateProvisionalKeysAsync(User user)
        {
            using var rsa = RSA.Create(2048);
            user.ProvisionalPrivateKey = rsa.ToXmlString(true);
            user.PublicKey = rsa.ToXmlString(false);
            
            // Note: We don't call SaveChanges here, the caller should.
            // But for GetUserPublicKey we might want to save immediately.
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync(); 
        }

        // POST: api/Users
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            if (await _context.Users.AnyAsync(u => u.Email == user.Email))
            {
                return Conflict("User with this email already exists.");
            }

            if (!string.IsNullOrEmpty(user.Password))
            {
                user.Password = _passwordHasher.HashPassword(user.Password);
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Notify clients
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "User", "Create", user.Id);
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"New user created: {user.FirstName} {user.LastName}");

            return CreatedAtAction("GetUser", new { id = user.Id }, user);
        }

        // PUT: api/Users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(Guid id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest();
            }

            var existingUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            // Allow Admin to update anyone, or user to update themselves
            if (currentUserId != null && id.ToString() != currentUserId && userRole != "Admin") 
            {
                return Forbid("You can only update your own profile.");
            }

            // Prevent changing own role or locking oneself out ideally, but simple for now
            if (existingUser != null && (existingUser.Email == "neil@mdk.co.za" || existingUser.Email == "neil@origize63.co.za"))
            {
                var currentUserEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value?.ToLowerInvariant();
                if (currentUserEmail != "neil@mdk.co.za" && currentUserEmail != "neil@origize63.co.za")
                {
                    return Forbid("Only the Developer can modify this account.");
                }
            }

            // If password provided, hash it
            
            // Note: AsNoTracking means the entity is not tracked. 
            // We need to attach the new 'user' or update properties.
            // Since we set State = Modified below, that attaches it.
            
            if (existingUser != null && user.Password != existingUser.Password) 
            {
                if (!string.IsNullOrEmpty(user.Password))
                {
                   user.Password = _passwordHasher.HashPassword(user.Password);
                }
                else 
                {
                    user.Password = existingUser.Password; // Keep old if empty
                }
            }

            if (existingUser != null)
            {
                // If PublicKey is being updated, it means rotation is happening or initial setup is complete.
                // Clear the provisional private key as it's no longer needed (the user has their own).
                if (!string.IsNullOrEmpty(user.PublicKey) && user.PublicKey != existingUser.PublicKey)
                {
                    user.ProvisionalPrivateKey = null;
                }
                // Otherwise, if not provided in request, preserve it (handles profile updates)
                else if (string.IsNullOrEmpty(user.ProvisionalPrivateKey))
                {
                    user.ProvisionalPrivateKey = existingUser.ProvisionalPrivateKey;
                }
            }

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "User", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id)) return NotFound();
                return Conflict("Another user has updated this record. Please reload and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {Id}", id);
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] OCC.Shared.DTOs.ChangePasswordRequest request)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var user = await _context.Users.FindAsync(Guid.Parse(userId));
            if (user == null) return NotFound("User not found.");

            // Verify old password
            if (!_passwordHasher.VerifyPassword(user.Password, request.OldPassword))
            {
                return BadRequest("Incorrect current password.");
            }

            // Set new password
            user.Password = _passwordHasher.HashPassword(request.NewPassword);
            
            await _context.SaveChangesAsync();
            return Ok("Password updated successfully.");
        }

        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound();
                }

                if (user.Email == "neil@mdk.co.za" || user.Email == "neil@origize63.co.za")
                {
                    return BadRequest("The Developer account cannot be deleted.");
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "User", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private bool UserExists(Guid id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
