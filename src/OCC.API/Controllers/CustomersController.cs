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
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CustomersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<CustomersController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet("summaries")]
        public async Task<ActionResult<IEnumerable<CustomerSummaryDto>>> GetCustomerSummaries()
        {
            try
            {
                return await _context.Customers
                    .OrderBy(c => c.Name)
                    .Select(c => new CustomerSummaryDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Header = c.Header,
                        Email = c.Email,
                        Phone = c.Phone,
                        Address = c.Address
                    })
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer summaries");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Customers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
        {
            try
            {
                return await _context.Customers.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Customers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Customer>> GetCustomer(Guid id)
        {
            try
            {
                var customer = await _context.Customers
                    .Include(c => c.Contacts)
                    .FirstOrDefaultAsync(c => c.Id == id);
                if (customer == null) return NotFound();
                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Customers
        [HttpPost]
        public async Task<ActionResult<Customer>> PostCustomer(Customer customer)
        {
            try
            {
                if (customer.Id == Guid.Empty) customer.Id = Guid.NewGuid();
                foreach (var contact in customer.Contacts)
                {
                    if (contact.Id == Guid.Empty) contact.Id = Guid.NewGuid();
                }

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Customer", "Create", customer.Id);

                return CreatedAtAction("GetCustomer", new { id = customer.Id }, customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Customers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCustomer(Guid id, Customer customer)
        {
            if (id != customer.Id) return BadRequest();

            try
            {
                // To properly handle disconnected graph including deleted children
                var existingCustomer = await _context.Customers
                    .Include(c => c.Contacts)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (existingCustomer == null) return NotFound();

                _context.Update(customer);

                // Delete missing contacts
                var clientContactIds = customer.Contacts.Select(c => c.Id).ToList();
                foreach (var existingContact in existingCustomer.Contacts)
                {
                    if (!clientContactIds.Contains(existingContact.Id))
                    {
                        var entry = _context.Entry(new CustomerContact { Id = existingContact.Id });
                        entry.State = EntityState.Deleted;
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
                return StatusCode(500, "Internal server error");
            }
            return NoContent();
        }

        // DELETE: api/Customers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(Guid id)
        {
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
                return StatusCode(500, "Internal server error");
            }
        }

        private bool CustomerExists(Guid id) => _context.Customers.Any(e => e.Id == id);
    }
}
