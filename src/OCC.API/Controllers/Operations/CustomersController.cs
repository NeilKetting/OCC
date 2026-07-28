using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using OCC.Shared.DTOs;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for customer accounts, contacts synchronization, and logo uploads.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CustomersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<CustomersController> _logger;

        private static readonly string[] AllowedLogoExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg" };
        private const long MaxLogoSizeBytes = 5 * 1024 * 1024; // 5 MB

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomersController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="hubContext">The SignalR hub context.</param>
        /// <param name="logger">The logger instance.</param>
        public CustomersController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<CustomersController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves light-weight customer summaries.
        /// </summary>
        /// <returns>A collection of customer summary DTOs.</returns>
        [HttpGet("summaries")]
        public async Task<ActionResult<IEnumerable<CustomerSummaryDto>>> GetCustomerSummaries()
        {
            try
            {
                var summaries = await _context.Customers
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Select(c => new CustomerSummaryDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Header = c.Header,
                        Email = c.Email,
                        Phone = c.Phone,
                        Address = c.Address,
                        LogoUrl = c.LogoUrl
                    })
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(summaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer summaries");
                return StatusCode(500, "An error occurred while retrieving customer summaries.");
            }
        }

        /// <summary>
        /// Retrieves all customers.
        /// </summary>
        /// <returns>A collection of customer entity objects.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
        {
            try
            {
                var customers = await _context.Customers.Where(c => c.IsActive).AsNoTracking().ToListAsync();
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers");
                return StatusCode(500, "An error occurred while retrieving customers.");
            }
        }

        /// <summary>
        /// Retrieves a specific customer by ID with contacts.
        /// </summary>
        /// <param name="id">The unique identifier of the customer.</param>
        /// <returns>The customer entity if found; otherwise, 404 Not Found.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Customer>> GetCustomer(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid customer ID.");

            try
            {
                var customer = await _context.Customers
                    .Include(c => c.Contacts)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (customer == null) return NotFound();
                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the customer.");
            }
        }

        /// <summary>
        /// Creates a new customer account.
        /// </summary>
        /// <param name="customer">The customer payload.</param>
        /// <returns>The created customer entity.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin,Office")]
        public async Task<ActionResult<Customer>> PostCustomer([FromBody] Customer customer)
        {
            if (customer == null) return BadRequest("Customer data is null.");

            if (string.IsNullOrWhiteSpace(customer.Name))
                return BadRequest("Customer name is required.");

            try
            {
                if (customer.Id == Guid.Empty) customer.Id = Guid.NewGuid();
                if (customer.Contacts != null)
                {
                    foreach (var contact in customer.Contacts)
                    {
                        if (contact.Id == Guid.Empty) contact.Id = Guid.NewGuid();
                    }
                }

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Customer", "Create", customer.Id);

                return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                return StatusCode(500, "An error occurred while creating the customer.");
            }
        }

        /// <summary>
        /// Updates an existing customer account and synchronizes contacts.
        /// </summary>
        /// <param name="id">The unique identifier of the customer to update.</param>
        /// <param name="customer">The updated customer payload.</param>
        /// <returns>No content on success; bad request, not found, or conflict on failure.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> PutCustomer(Guid id, [FromBody] Customer customer)
        {
            if (customer == null || id != customer.Id) return BadRequest("Customer ID mismatch or payload is null.");

            if (string.IsNullOrWhiteSpace(customer.Name))
                return BadRequest("Customer name is required.");

            try
            {
                var existingCustomer = await _context.Customers
                    .Include(c => c.Contacts)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (existingCustomer == null) return NotFound();

                _context.Entry(existingCustomer).CurrentValues.SetValues(customer);

                // Delete missing contacts
                var clientContactIds = (customer.Contacts ?? new List<CustomerContact>()).Select(c => c.Id).ToList();
                var contactsToRemove = existingCustomer.Contacts.Where(c => !clientContactIds.Contains(c.Id)).ToList();

                foreach (var contactToRemove in contactsToRemove)
                {
                    _context.CustomerContacts.Remove(contactToRemove);
                }

                foreach (var contact in customer.Contacts ?? new List<CustomerContact>())
                {
                    var existing = existingCustomer.Contacts.FirstOrDefault(c => c.Id == contact.Id);
                    if (existing != null)
                    {
                        _context.Entry(existing).CurrentValues.SetValues(contact);
                    }
                    else
                    {
                        if (contact.Id == Guid.Empty) contact.Id = Guid.NewGuid();
                        contact.CustomerId = id;
                        existingCustomer.Contacts.Add(contact);
                    }
                }

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Customer", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(id)) return NotFound();
                return Conflict("Another user has updated this record. Please reload and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer {Id}", id);
                return StatusCode(500, "An error occurred while updating the customer.");
            }
            return NoContent();
        }

        /// <summary>
        /// Deletes a customer account by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the customer to delete.</param>
        /// <returns>No content on success; 404 Not Found if customer is missing.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> DeleteCustomer(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid customer ID.");

            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null) return NotFound();
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Customer", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer {Id}", id);
                return StatusCode(500, "An error occurred while deleting the customer.");
            }
        }

        /// <summary>
        /// Securely uploads and associates a logo image for a customer account.
        /// </summary>
        /// <param name="id">The unique identifier of the customer.</param>
        /// <param name="file">The uploaded image file payload.</param>
        /// <returns>The updated logo URL relative path.</returns>
        [HttpPost("{id}/upload-logo")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<ActionResult<string>> UploadLogo(Guid id, IFormFile file)
        {
            if (id == Guid.Empty) return BadRequest("Invalid customer ID.");

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            if (file.Length > MaxLogoSizeBytes)
            {
                return BadRequest("Uploaded logo image exceeds maximum allowed size (5MB).");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedLogoExtensions.Contains(extension))
            {
                return BadRequest("Invalid image file type. Allowed extensions: .jpg, .jpeg, .png, .webp, .gif, .svg");
            }

            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                {
                    return NotFound("Customer not found.");
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "customer_logos");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Sanitize file name to prevent path traversal
                var safeExtension = extension;
                var uniqueFileName = $"{id}_{Guid.NewGuid()}{safeExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                customer.LogoUrl = $"/uploads/customer_logos/{uniqueFileName}";
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Customer", "Update", id);

                return Ok(customer.LogoUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading logo for customer {Id}", id);
                return StatusCode(500, "An error occurred while uploading the logo.");
            }
        }

        private bool CustomerExists(Guid id) => _context.Customers.Any(e => e.Id == id);
    }
}
