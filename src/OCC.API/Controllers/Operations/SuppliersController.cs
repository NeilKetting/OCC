using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using OCC.Shared.DTOs;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for supplier management, directory listing, and contacts synchronization.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SuppliersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SuppliersController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SuppliersController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="hubContext">The SignalR hub context.</param>
        /// <param name="logger">The logger instance.</param>
        public SuppliersController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<SuppliersController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves light-weight supplier summaries.
        /// </summary>
        /// <returns>A collection of supplier summary DTOs.</returns>
        [HttpGet("summaries")]
        public async Task<ActionResult<IEnumerable<SupplierSummaryDto>>> GetSupplierSummaries()
        {
            try
            {
                var summaries = await _context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .Select(s => new SupplierSummaryDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Email = s.Email,
                        Phone = s.Phone,
                        Branch = s.Branch.ToString(),
                        VatNumber = s.VatNumber,
                        ContactPerson = s.ContactPerson,
                        Address = s.Address != null ? s.Address.Replace("\r\n", ", ").Replace("\n", ", ").Replace("\r", ", ") : string.Empty,
                        City = s.City,
                        PostalCode = s.PostalCode,
                        BankName = s.BankName,
                        BankAccountNumber = s.BankAccountNumber,
                        BranchCode = s.BranchCode,
                        SupplierAccountNumber = s.SupplierAccountNumber
                    })
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(summaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supplier summaries");
                return StatusCode(500, "An error occurred while retrieving supplier summaries.");
            }
        }

        /// <summary>
        /// Retrieves all suppliers with contacts.
        /// </summary>
        /// <returns>A collection of suppliers.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Supplier>>> GetSuppliers()
        {
            try
            {
                var suppliers = await _context.Suppliers
                    .Where(s => s.IsActive)
                    .Include(s => s.Contacts)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(suppliers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving suppliers");
                return StatusCode(500, "An error occurred while retrieving suppliers.");
            }
        }

        /// <summary>
        /// Retrieves a specific supplier by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the supplier.</param>
        /// <returns>The supplier entity if found; otherwise, 404 Not Found.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Supplier>> GetSupplier(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid supplier ID.");

            try
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.Contacts)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == id);
                
                if (supplier == null) return NotFound();
                return Ok(supplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supplier {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the supplier.");
            }
        }

        /// <summary>
        /// Creates a new supplier.
        /// </summary>
        /// <param name="supplier">The supplier payload.</param>
        /// <returns>The created supplier object.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin,Office")]
        public async Task<ActionResult<Supplier>> PostSupplier([FromBody] Supplier supplier)
        {
            if (supplier == null) return BadRequest("Supplier data is null.");

            if (string.IsNullOrWhiteSpace(supplier.Name))
                return BadRequest("Supplier name is required.");

            try
            {
                if (supplier.Id == Guid.Empty) supplier.Id = Guid.NewGuid();
                if (supplier.Contacts != null)
                {
                    foreach (var c in supplier.Contacts)
                    {
                        if (c.Id == Guid.Empty) c.Id = Guid.NewGuid();
                        c.SupplierId = supplier.Id;
                    }
                }
                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Supplier", "Create", supplier.Id);

                return CreatedAtAction(nameof(GetSupplier), new { id = supplier.Id }, supplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating supplier");
                return StatusCode(500, "An error occurred while creating the supplier.");
            }
        }

        /// <summary>
        /// Updates an existing supplier.
        /// </summary>
        /// <param name="id">The unique identifier of the supplier to update.</param>
        /// <param name="supplier">The updated supplier payload.</param>
        /// <returns>No content on success; bad request or not found on failure.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> PutSupplier(Guid id, [FromBody] Supplier supplier)
        {
            if (supplier == null || id != supplier.Id) return BadRequest("Supplier ID mismatch or payload is null.");

            if (string.IsNullOrWhiteSpace(supplier.Name))
                return BadRequest("Supplier name is required.");

            var existingSupplier = await _context.Suppliers.FindAsync(id);
            if (existingSupplier == null)
            {
                return NotFound();
            }

            _context.Entry(existingSupplier).CurrentValues.SetValues(supplier);

            // Sync Contacts
            var existingContacts = await _context.SupplierContacts.Where(c => c.SupplierId == id).ToListAsync();
            _context.SupplierContacts.RemoveRange(existingContacts);

            if (supplier.Contacts != null && supplier.Contacts.Count > 0)
            {
                foreach (var c in supplier.Contacts)
                {
                    if (c.Id == Guid.Empty) c.Id = Guid.NewGuid();
                    c.SupplierId = id;
                    _context.SupplierContacts.Add(c);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Supplier", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SupplierExists(id)) return NotFound();
                else throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating supplier {Id}", id);
                return StatusCode(500, "An error occurred while updating the supplier.");
            }
            return NoContent();
        }

        /// <summary>
        /// Deletes a supplier by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the supplier to delete.</param>
        /// <returns>No content on success; 404 Not Found if supplier does not exist.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> DeleteSupplier(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid supplier ID.");

            try
            {
                var supplier = await _context.Suppliers.FindAsync(id);
                if (supplier == null) return NotFound();
                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Supplier", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting supplier {Id}", id);
                return StatusCode(500, "An error occurred while deleting the supplier.");
            }
        }

        private bool SupplierExists(Guid id) => _context.Suppliers.Any(e => e.Id == id);
    }
}
