using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Security;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers.Projects
{
    /// <summary>
    /// API Controller for managing project variation orders (VOs), contract change requests, and approval statuses.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProjectVariationOrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProjectVariationOrdersController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectVariationOrdersController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="logger">Logger instance.</param>
        public ProjectVariationOrdersController(AppDbContext context, ILogger<ProjectVariationOrdersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all variation orders, optionally filtered by project ID.
        /// </summary>
        /// <param name="projectId">Optional project ID filter.</param>
        /// <returns>A list of project variation orders.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectVariationOrder>>> GetVariationOrders([FromQuery] Guid? projectId = null)
        {
            try
            {
                var query = _context.ProjectVariationOrders.AsQueryable();
                
                if (projectId.HasValue)
                {
                    if (projectId.Value == Guid.Empty) return BadRequest("Invalid project ID.");
                    query = query.Where(v => v.ProjectId == projectId.Value);
                }

                return Ok(await query.ToListAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variation orders for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while retrieving variation orders.");
            }
        }

        /// <summary>
        /// Retrieves a single variation order by its ID.
        /// </summary>
        /// <param name="id">The variation order ID.</param>
        /// <returns>The requested variation order entity.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectVariationOrder>> GetVariationOrder(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid variation order ID.");

            try
            {
                var variationOrder = await _context.ProjectVariationOrders.FindAsync(id);

                if (variationOrder == null)
                {
                    return NotFound();
                }

                return Ok(variationOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variation order {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the variation order.");
            }
        }

        /// <summary>
        /// Creates a new variation order entity.
        /// </summary>
        /// <param name="variationOrder">The variation order entity.</param>
        /// <returns>The created variation order.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<ActionResult<ProjectVariationOrder>> PostVariationOrder(ProjectVariationOrder variationOrder)
        {
            if (variationOrder == null) return BadRequest("Variation order payload cannot be null.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                if (variationOrder.Id == Guid.Empty) variationOrder.Id = Guid.NewGuid();

                // Input sanitization
                variationOrder.Description = InputSanitizer.Sanitize(variationOrder.Description);
                variationOrder.ApprovedBy = InputSanitizer.Sanitize(variationOrder.ApprovedBy);
                variationOrder.AdditionalComments = InputSanitizer.Sanitize(variationOrder.AdditionalComments);
                variationOrder.Status = InputSanitizer.Sanitize(variationOrder.Status);
                variationOrder.DurationDays = Math.Max(0, variationOrder.DurationDays);

                _context.ProjectVariationOrders.Add(variationOrder);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetVariationOrder), new { id = variationOrder.Id }, variationOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating variation order");
                return StatusCode(500, "An error occurred while creating the variation order.");
            }
        }

        /// <summary>
        /// Updates an existing variation order entity.
        /// </summary>
        /// <param name="id">The variation order ID.</param>
        /// <param name="variationOrder">The updated variation order entity.</param>
        /// <returns>No content on success.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> PutVariationOrder(Guid id, ProjectVariationOrder variationOrder)
        {
            if (id == Guid.Empty || id != variationOrder.Id)
            {
                return BadRequest("Variation order ID mismatch or empty.");
            }
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existingVO = await _context.ProjectVariationOrders.FindAsync(id);
            if (existingVO == null)
            {
                return NotFound();
            }

            // Input sanitization
            variationOrder.Description = InputSanitizer.Sanitize(variationOrder.Description);
            variationOrder.ApprovedBy = InputSanitizer.Sanitize(variationOrder.ApprovedBy);
            variationOrder.AdditionalComments = InputSanitizer.Sanitize(variationOrder.AdditionalComments);
            variationOrder.Status = InputSanitizer.Sanitize(variationOrder.Status);
            variationOrder.DurationDays = Math.Max(0, variationOrder.DurationDays);

            _context.Entry(existingVO).CurrentValues.SetValues(variationOrder);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VariationOrderExists(id))
                {
                    return NotFound();
                }
                else
                {
                    return Conflict("Another user has updated this record. Please reload and try again.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating variation order {Id}", id);
                return StatusCode(500, "An error occurred while updating the variation order.");
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes a variation order by its ID.
        /// </summary>
        /// <param name="id">The variation order ID to delete.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> DeleteVariationOrder(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid variation order ID.");

            try
            {
                var variationOrder = await _context.ProjectVariationOrders.FindAsync(id);
                if (variationOrder == null)
                {
                    return NotFound();
                }

                _context.ProjectVariationOrders.Remove(variationOrder);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting variation order {Id}", id);
                return StatusCode(500, "An error occurred while deleting the variation order.");
            }
        }

        private bool VariationOrderExists(Guid id)
        {
            return _context.ProjectVariationOrders.Any(e => e.Id == id);
        }
    }
}
