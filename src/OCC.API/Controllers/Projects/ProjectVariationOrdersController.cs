using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;

namespace OCC.API.Controllers.Projects
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProjectVariationOrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProjectVariationOrdersController> _logger;

        public ProjectVariationOrdersController(AppDbContext context, ILogger<ProjectVariationOrdersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectVariationOrder>>> GetVariationOrders(Guid? projectId = null)
        {
            try
            {
                var query = _context.ProjectVariationOrders.AsQueryable();
                
                if (projectId.HasValue)
                {
                    query = query.Where(v => v.ProjectId == projectId.Value);
                }

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variation orders for project {ProjectId}", projectId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectVariationOrder>> GetVariationOrder(Guid id)
        {
            try
            {
                var variationOrder = await _context.ProjectVariationOrders.FindAsync(id);

                if (variationOrder == null)
                {
                    return NotFound();
                }

                return variationOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variation order {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<ProjectVariationOrder>> PostVariationOrder(ProjectVariationOrder variationOrder)
        {
            try
            {
                if (variationOrder.Id == Guid.Empty) variationOrder.Id = Guid.NewGuid();
                _context.ProjectVariationOrders.Add(variationOrder);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetVariationOrder), new { id = variationOrder.Id }, variationOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating variation order");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutVariationOrder(Guid id, ProjectVariationOrder variationOrder)
        {
            if (id != variationOrder.Id)
            {
                return BadRequest();
            }

            _context.Entry(variationOrder).State = EntityState.Modified;

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
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating variation order {Id}", id);
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVariationOrder(Guid id)
        {
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
                return StatusCode(500, "Internal server error");
            }
        }

        private bool VariationOrderExists(Guid id)
        {
            return _context.ProjectVariationOrders.Any(e => e.Id == id);
        }
    }
}
