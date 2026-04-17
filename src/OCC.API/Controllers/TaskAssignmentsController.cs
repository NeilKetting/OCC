using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TaskAssignmentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<TaskAssignmentsController> _logger;

        public TaskAssignmentsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<TaskAssignmentsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskAssignment>>> GetTaskAssignments(Guid? taskId = null)
        {
            try
            {
                var query = _context.TaskAssignments.AsQueryable();
                if (taskId.HasValue) query = query.Where(a => a.TaskId == taskId.Value);
                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting assignments");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TaskAssignment>> GetTaskAssignment(Guid id)
        {
            try
            {
                var assignment = await _context.TaskAssignments.FindAsync(id);
                if (assignment == null) return NotFound();
                return assignment;
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error retrieving assignment {Id}", id);
                 return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<TaskAssignment>> PostTaskAssignment(TaskAssignment assignment)
        {
            try
            {
                if (assignment.Id == Guid.Empty) assignment.Id = Guid.NewGuid();
                _context.TaskAssignments.Add(assignment);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "TaskAssignment", "Create", assignment.Id);

                return CreatedAtAction("GetTaskAssignment", new { id = assignment.Id }, assignment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating assignment");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTaskAssignment(Guid id)
        {
            try
            {
                var assignment = await _context.TaskAssignments.FindAsync(id);
                if (assignment == null) return NotFound();
                _context.TaskAssignments.Remove(assignment);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "TaskAssignment", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error deleting assignment {Id}", id);
                 return StatusCode(500, "Internal server error");
            }
        }
    }
}
