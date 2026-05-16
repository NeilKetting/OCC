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
        private readonly Services.INotificationService _notificationService;
        private readonly ILogger<TaskAssignmentsController> _logger;

        public TaskAssignmentsController(AppDbContext context, IHubContext<NotificationHub> hubContext, Services.INotificationService notificationService, ILogger<TaskAssignmentsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _notificationService = notificationService;
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

                // Notify Assignee
                await NotifyAssigneeAsync(assignment);

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

        private async Task NotifyAssigneeAsync(TaskAssignment assignment)
        {
            try
            {
                Guid? targetUserId = null;
                string taskName = "a new task";

                // 1. Resolve Task Name
                var task = await _context.ProjectTasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == assignment.TaskId);
                if (task != null) taskName = task.Name;

                // 2. Resolve target User ID
                if (assignment.AssigneeType == AssigneeType.Staff)
                {
                    var employee = await _context.Employees.FindAsync(assignment.AssigneeId);
                    if (employee != null)
                    {
                        targetUserId = employee.LinkedUserId;
                        
                        // Fallback: Link by Email
                        if (!targetUserId.HasValue && !string.IsNullOrEmpty(employee.Email))
                        {
                            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == employee.Email);
                            if (user != null)
                            {
                                targetUserId = user.Id;
                                employee.LinkedUserId = user.Id;
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }
                else if (assignment.AssigneeType == AssigneeType.Contractor)
                {
                    // Check if AssigneeId is a direct User ID
                    if (await _context.Users.AnyAsync(u => u.Id == assignment.AssigneeId))
                    {
                        targetUserId = assignment.AssigneeId;
                    }
                    else
                    {
                        // Check if it's a SubContractor ID
                        var contractor = await _context.SubContractors.FindAsync(assignment.AssigneeId);
                        if (contractor != null)
                        {
                            targetUserId = contractor.PortalUserId;
                        }
                    }
                }

                if (targetUserId.HasValue)
                {
                    var projectTitle = task?.Project?.Name ?? "a project";
                    await _notificationService.SendPushNotificationAsync(
                        targetUserId.Value,
                        "New Task Assigned",
                        $"You have been assigned to task: '{taskName}' in project: '{projectTitle}'"
                    );
                    _logger.LogInformation("Sent task assignment push to User {UserId}", targetUserId.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification for task assignment");
            }
        }
    }
}
