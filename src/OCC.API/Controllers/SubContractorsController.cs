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
    public class SubContractorsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SubContractorsController> _logger;

        public SubContractorsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<SubContractorsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet("summaries")]
        public async Task<ActionResult<IEnumerable<SubContractorSummaryDto>>> GetSubContractorSummaries()
        {
            try
            {
                return await _context.SubContractors
                    .OrderBy(c => c.Name)
                    .Select(c => new SubContractorSummaryDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Email = c.Email,
                        Phone = c.Phone,
                        Specialties = c.Specialties,
                        Branch = c.Branch
                    })
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sub-contractor summaries");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/SubContractors
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SubContractor>>> GetSubContractors()
        {
            try
            {
                return await _context.SubContractors.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sub-contractors");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/SubContractors/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SubContractor>> GetSubContractor(Guid id)
        {
            try
            {
                var subContractor = await _context.SubContractors.FirstOrDefaultAsync(c => c.Id == id);
                if (subContractor == null) return NotFound();
                return subContractor;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sub-contractor {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/SubContractors
        [HttpPost]
        public async Task<ActionResult<SubContractor>> PostSubContractor(SubContractor subContractor)
        {
            try
            {
                if (subContractor.Id == Guid.Empty) subContractor.Id = Guid.NewGuid();

                _context.SubContractors.Add(subContractor);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SubContractor", "Create", subContractor.Id);

                return CreatedAtAction("GetSubContractor", new { id = subContractor.Id }, subContractor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sub-contractor");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/SubContractors/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSubContractor(Guid id, SubContractor subContractor)
        {
            if (id != subContractor.Id) return BadRequest();

            try
            {
                _context.Update(subContractor);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SubContractor", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SubContractorExists(id)) return NotFound();
                return Conflict("Another user has updated this record. Please reload and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sub-contractor {Id}", id);
                return StatusCode(500, "Internal server error");
            }
            return NoContent();
        }

        // DELETE: api/SubContractors/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubContractor(Guid id)
        {
            try
            {
                var subContractor = await _context.SubContractors.FindAsync(id);
                if (subContractor == null) return NotFound();
                _context.SubContractors.Remove(subContractor);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SubContractor", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sub-contractor {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private bool SubContractorExists(Guid id) => _context.SubContractors.Any(e => e.Id == id);
    }
}
