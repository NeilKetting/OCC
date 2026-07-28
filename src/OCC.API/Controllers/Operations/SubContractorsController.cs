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
    /// API Controller for subcontractor operations, directory listing, and tier management.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SubContractorsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SubContractorsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubContractorsController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="hubContext">The SignalR hub context.</param>
        /// <param name="logger">The logger instance.</param>
        public SubContractorsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<SubContractorsController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves light-weight subcontractor summaries.
        /// </summary>
        /// <returns>A collection of subcontractor summary DTOs.</returns>
        [HttpGet("summaries")]
        public async Task<ActionResult<IEnumerable<SubContractorSummaryDto>>> GetSubContractorSummaries()
        {
            try
            {
                var summaries = await _context.SubContractors
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Select(c => new SubContractorSummaryDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Email = c.Email,
                        Phone = c.Phone,
                        Specialties = c.Specialties,
                        Branch = c.Branch,
                        PerformanceTier = c.PerformanceTier,
                        ColorTheme = c.ColorTheme
                    })
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(summaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sub-contractor summaries");
                return StatusCode(500, "An error occurred while retrieving sub-contractor summaries.");
            }
        }

        /// <summary>
        /// Retrieves all subcontractors.
        /// </summary>
        /// <returns>A collection of subcontractor entity objects.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SubContractor>>> GetSubContractors()
        {
            try
            {
                var subs = await _context.SubContractors.Where(c => c.IsActive).AsNoTracking().ToListAsync();
                return Ok(subs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sub-contractors");
                return StatusCode(500, "An error occurred while retrieving sub-contractors.");
            }
        }

        /// <summary>
        /// Retrieves a specific subcontractor by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the subcontractor.</param>
        /// <returns>The subcontractor entity if found; otherwise, 404 Not Found.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<SubContractor>> GetSubContractor(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid sub-contractor ID.");

            try
            {
                var subContractor = await _context.SubContractors.FirstOrDefaultAsync(c => c.Id == id);
                if (subContractor == null) return NotFound();
                return Ok(subContractor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sub-contractor {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the sub-contractor.");
            }
        }

        /// <summary>
        /// Creates a new subcontractor.
        /// </summary>
        /// <param name="subContractor">The subcontractor payload.</param>
        /// <returns>The created subcontractor object.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin,Office")]
        public async Task<ActionResult<SubContractor>> PostSubContractor([FromBody] SubContractor subContractor)
        {
            if (subContractor == null) return BadRequest("SubContractor data is null.");

            if (string.IsNullOrWhiteSpace(subContractor.Name))
                return BadRequest("SubContractor name is required.");

            try
            {
                if (subContractor.Id == Guid.Empty) subContractor.Id = Guid.NewGuid();

                _logger.LogInformation("Creating SubContractor {Name} with ColorTheme: {ColorTheme}", subContractor.Name, subContractor.ColorTheme);

                _context.SubContractors.Add(subContractor);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SubContractor", "Create", subContractor.Id);

                return CreatedAtAction(nameof(GetSubContractor), new { id = subContractor.Id }, subContractor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sub-contractor");
                return StatusCode(500, "An error occurred while creating the sub-contractor.");
            }
        }

        /// <summary>
        /// Updates an existing subcontractor.
        /// </summary>
        /// <param name="id">The unique identifier of the subcontractor to update.</param>
        /// <param name="subContractor">The updated subcontractor payload.</param>
        /// <returns>No content on success; bad request, not found, or conflict on failure.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> PutSubContractor(Guid id, [FromBody] SubContractor subContractor)
        {
            if (subContractor == null || id != subContractor.Id) return BadRequest("SubContractor ID mismatch or payload is null.");

            if (string.IsNullOrWhiteSpace(subContractor.Name))
                return BadRequest("SubContractor name is required.");

            var existingSub = await _context.SubContractors.FindAsync(id);
            if (existingSub == null)
            {
                return NotFound();
            }

            _context.Entry(existingSub).CurrentValues.SetValues(subContractor);

            try
            {
                _logger.LogInformation("Updating SubContractor {Id} with ColorTheme: {ColorTheme}", id, subContractor.ColorTheme);

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
                return StatusCode(500, "An error occurred while updating the sub-contractor.");
            }
            return NoContent();
        }

        /// <summary>
        /// Deletes a subcontractor by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the subcontractor to delete.</param>
        /// <returns>No content on success; 404 Not Found if subcontractor is missing.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> DeleteSubContractor(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid sub-contractor ID.");

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
                return StatusCode(500, "An error occurred while deleting the sub-contractor.");
            }
        }

        private bool SubContractorExists(Guid id) => _context.SubContractors.Any(e => e.Id == id);
    }
}
