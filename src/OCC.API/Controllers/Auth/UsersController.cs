using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.API.Services;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for managing user profiles, contacts, provisional encryption keys, and password changes.
    /// </summary>
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILogger<UsersController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="passwordHasher">The password hashing service.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="hubContext">The SignalR notification hub context.</param>
        public UsersController(
            AppDbContext context,
            IPasswordHasher passwordHasher,
            ILogger<UsersController> logger,
            IHubContext<NotificationHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext;
        }

        /// <summary>
        /// Retrieves all registered system users (Admin role required).
        /// </summary>
        /// <returns>A list of system users.</returns>
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

        /// <summary>
        /// Retrieves a user by unique identifier. Users may view their own profile; Admins may view any profile.
        /// </summary>
        /// <param name="id">The user ID.</param>
        /// <returns>The matching user entity.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(Guid id)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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

        /// <summary>
        /// Retrieves or generates the public encryption key for a specific user for E2EE chat.
        /// </summary>
        /// <param name="id">The target user ID.</param>
        /// <returns>Object containing the public key.</returns>
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

        /// <summary>
        /// Retrieves the provisional private key for the currently authenticated user during onboarding.
        /// </summary>
        /// <returns>Object containing the provisional private key.</returns>
        [HttpGet("me/provisional-key")]
        public async Task<ActionResult<string>> GetMyProvisionalKey()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out var userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound();

                return Ok(new { ProvisionalPrivateKey = user.ProvisionalPrivateKey });
            }
            return Unauthorized();
        }

        /// <summary>
        /// Retrieves contact list for secure messaging excluding the caller.
        /// </summary>
        /// <returns>List of contact user details with public keys.</returns>
        [HttpGet("contacts")]
        public async Task<ActionResult<IEnumerable<ChatUserDto>>> GetContacts()
        {
            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            try
            {
                Guid.TryParse(currentUserIdStr, out var currentUserId);
                var users = await _context.Users
                    .Where(u => u.Id != currentUserId)
                    .ToListAsync();

                var contacts = new List<ChatUserDto>();
                bool anyGenerated = false;

                foreach (var u in users)
                {
                    if (string.IsNullOrEmpty(u.PublicKey))
                    {
                        await GenerateProvisionalKeysAsync(u);
                        anyGenerated = true;
                    }

                    contacts.Add(new ChatUserDto
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

        /// <summary>
        /// Creates a new user (Admin role required).
        /// </summary>
        /// <param name="user">The user entity to create.</param>
        /// <returns>201 CreatedAtAction with the user record.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return BadRequest("Invalid user data.");
            }

            if (await _context.Users.AnyAsync(u => u.Email == user.Email))
            {
                return Conflict("User with this email already exists.");
            }

            if (!string.IsNullOrEmpty(user.Password))
            {
                if (!_passwordHasher.IsPasswordComplex(user.Password))
                {
                    return BadRequest("Password does not meet complexity requirements.");
                }
                user.Password = _passwordHasher.HashPassword(user.Password);
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            if (_hubContext != null)
            {
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "User", "Create", user.Id);
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"New user created: {user.FirstName} {user.LastName}");
            }

            return CreatedAtAction("GetUser", new { id = user.Id }, user);
        }

        /// <summary>
        /// Updates an existing user profile.
        /// </summary>
        /// <param name="id">The user ID matching the request path.</param>
        /// <param name="user">The updated user entity.</param>
        /// <returns>204 NoContent if successful; 400 BadRequest or 403 Forbidden on failure.</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(Guid id, User user)
        {
            if (user == null || id != user.Id)
            {
                return BadRequest();
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (existingUser == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserId != null && id.ToString() != currentUserId && userRole != "Admin")
            {
                return Forbid("You can only update your own profile.");
            }

            if (existingUser.Email == "neil@mdk.co.za" || existingUser.Email == "neil@origize63.co.za")
            {
                var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value?.ToLowerInvariant();
                if (currentUserEmail != "neil@mdk.co.za" && currentUserEmail != "neil@origize63.co.za")
                {
                    return Forbid("Only the Developer can modify this account.");
                }
            }

            if (!string.IsNullOrEmpty(user.Password) && user.Password != existingUser.Password)
            {
                if (!_passwordHasher.IsPasswordComplex(user.Password))
                {
                    return BadRequest("Password does not meet complexity requirements.");
                }
                user.Password = _passwordHasher.HashPassword(user.Password);
            }
            else
            {
                user.Password = existingUser.Password;
            }

            if (!string.IsNullOrEmpty(user.PublicKey) && user.PublicKey != existingUser.PublicKey)
            {
                user.ProvisionalPrivateKey = null;
            }
            else if (string.IsNullOrEmpty(user.ProvisionalPrivateKey))
            {
                user.ProvisionalPrivateKey = existingUser.ProvisionalPrivateKey;
            }

            _context.Entry(existingUser).CurrentValues.SetValues(user);

            try
            {
                await _context.SaveChangesAsync();
                if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("EntityUpdate", "User", "Update", id);
                }
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

        /// <summary>
        /// Changes the password for the currently authenticated user.
        /// </summary>
        /// <param name="request">The password change request containing old and new passwords.</param>
        /// <returns>200 OK if successful; 400 BadRequest if current password is wrong or new password violates rules.</returns>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest("Current password and new password are required.");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var user = await _context.Users.FindAsync(Guid.Parse(userId));
            if (user == null) return NotFound("User not found.");

            if (!_passwordHasher.VerifyPassword(request.OldPassword, user.Password))
            {
                return BadRequest("Incorrect current password.");
            }

            if (!_passwordHasher.IsPasswordComplex(request.NewPassword))
            {
                return BadRequest("Password does not meet OWASP complexity requirements. Minimum 8 characters, containing uppercase, lowercase, and a digit or special character.");
            }

            user.Password = _passwordHasher.HashPassword(request.NewPassword);

            await _context.SaveChangesAsync();
            return Ok("Password updated successfully.");
        }

        /// <summary>
        /// Deletes a user account (Admin role required).
        /// </summary>
        /// <param name="id">The ID of the user to delete.</param>
        /// <returns>240 NoContent if successful; 400 BadRequest or 404 NotFound on error.</returns>
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
                    var otherDeveloperExists = await _context.Users
                        .AnyAsync(u => u.Email == user.Email && u.Id != id && u.IsActive);

                    if (!otherDeveloperExists)
                    {
                        return BadRequest("The Developer account cannot be deleted.");
                    }
                }

                var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(currentUserIdStr, out var currentUserId) && currentUserId == id)
                {
                    return BadRequest("You cannot delete your own active account.");
                }

                var linkedEmployees = await _context.Employees.Where(e => e.LinkedUserId == id).ToListAsync();
                foreach (var employee in linkedEmployees)
                {
                    employee.LinkedUserId = null;
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("EntityUpdate", "User", "Delete", id);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task GenerateProvisionalKeysAsync(User user)
        {
            using var rsa = RSA.Create(2048);
            user.ProvisionalPrivateKey = rsa.ToXmlString(true);
            user.PublicKey = rsa.ToXmlString(false);

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        private bool UserExists(Guid id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
