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
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SuppliersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SuppliersController> _logger;

        public SuppliersController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<SuppliersController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet("summaries")]
        public async Task<ActionResult<IEnumerable<SupplierSummaryDto>>> GetSupplierSummaries()
        {
            try
            {
                return await _context.Suppliers
                    .OrderBy(s => s.Name)
                    .Select(s => new SupplierSummaryDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Email = s.Email,
                        Phone = s.Phone,
                        Branch = s.Branch.ToString(),
                        VatNumber = s.VatNumber
                    })
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supplier summaries");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Suppliers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Supplier>>> GetSuppliers()
        {
            try
            {
                return await _context.Suppliers.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving suppliers");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Suppliers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Supplier>> GetSupplier(Guid id)
        {
            try
            {
                var supplier = await _context.Suppliers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == id);
                
                if (supplier == null) return NotFound();
                return supplier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supplier {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Suppliers
        [HttpPost]
        public async Task<ActionResult<Supplier>> PostSupplier(Supplier supplier)
        {
            try
            {
                if (supplier.Id == Guid.Empty) supplier.Id = Guid.NewGuid();
                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Supplier", "Create", supplier.Id);

                return CreatedAtAction("GetSupplier", new { id = supplier.Id }, supplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating supplier");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Suppliers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSupplier(Guid id, Supplier supplier)
        {
            if (id != supplier.Id) return BadRequest();
            _context.Entry(supplier).State = EntityState.Modified;

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
                return StatusCode(500, "Internal server error");
            }
            return NoContent();
        }

        // DELETE: api/Suppliers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSupplier(Guid id)
        {
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
                return StatusCode(500, "Internal server error");
            }
        }

        private bool SupplierExists(Guid id) => _context.Suppliers.Any(e => e.Id == id);
    }
}
