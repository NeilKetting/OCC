using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for managing task assignments to staff, contractors, and teams, and triggering push notifications.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TaskAssignmentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly Services.INotificationService _notificationService;
        private readonly ILogger<TaskAssignmentsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskAssignmentsController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="hubContext">SignalR notification hub context.</param>
        /// <param name="notificationService">Notification service.</param>
        /// <param name="logger">Logger instance.</param>
        public TaskAssignmentsController(AppDbContext context, IHubContext<NotificationHub> hubContext, Services.INotificationService notificationService, ILogger<TaskAssignmentsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves task assignments, optionally filtered by task ID.
        /// </summary>
        /// <param name="taskId">Optional task ID filter.</param>
        /// <returns>A list of task assignments.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskAssignment>>> GetTaskAssignments([FromQuery] Guid? taskId = null)
        {
            try
            {
                var query = _context.TaskAssignments.AsQueryable();
                if (taskId.HasValue)
                {
                    if (taskId.Value == Guid.Empty) return BadRequest("Invalid task ID.");
                    query = query.Where(a => a.TaskId == taskId.Value);
                }
                return Ok(await query.ToListAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting assignments");
                return StatusCode(500, "An error occurred while retrieving task assignments.");
            }
        }

        /// <summary>
        /// Retrieves a single task assignment by its unique identifier.
        /// </summary>
        /// <param name="id">The assignment ID.</param>
        /// <returns>The requested task assignment entity.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<TaskAssignment>> GetTaskAssignment(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid assignment ID.");

            try
            {
                var assignment = await _context.TaskAssignments.FindAsync(id);
                if (assignment == null) return NotFound();
                return Ok(assignment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assignment {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the task assignment.");
            }
        }

        /// <summary>
        /// Creates a new task assignment and dispatches a push notification to the assignee.
        /// </summary>
        /// <param name="assignment">The task assignment entity.</param>
        /// <returns>The created task assignment entity.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<ActionResult<TaskAssignment>> PostTaskAssignment(TaskAssignment assignment)
        {
            if (assignment == null) return BadRequest("Assignment payload cannot be null.");
            if (assignment.TaskId == Guid.Empty) return BadRequest("Invalid task ID.");
            if (assignment.AssigneeId == Guid.Empty) return BadRequest("Invalid assignee ID.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                if (assignment.Id == Guid.Empty) assignment.Id = Guid.NewGuid();
                _context.TaskAssignments.Add(assignment);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "TaskAssignment", "Create", assignment.Id);

                await NotifyAssigneeAsync(assignment);

                return CreatedAtAction("GetTaskAssignment", new { id = assignment.Id }, assignment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating assignment");
                return StatusCode(500, "An error occurred while creating the task assignment.");
            }
        }

        /// <summary>
        /// Deletes a task assignment by its ID.
        /// </summary>
        /// <param name="id">The assignment ID to delete.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> DeleteTaskAssignment(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid assignment ID.");

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
                return StatusCode(500, "An error occurred while deleting the task assignment.");
            }
        }

        private async Task NotifyAssigneeAsync(TaskAssignment assignment)
        {
            try
            {
                Guid? targetUserId = null;
                string taskName = "a new task";

                var task = await _context.ProjectTasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == assignment.TaskId);
                if (task != null) taskName = task.Name;

                if (assignment.AssigneeType == AssigneeType.Staff)
                {
                    var employee = await _context.Employees.FindAsync(assignment.AssigneeId);
                    if (employee != null)
                    {
                        targetUserId = employee.LinkedUserId;
                        
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
                    if (await _context.Users.AnyAsync(u => u.Id == assignment.AssigneeId))
                    {
                        targetUserId = assignment.AssigneeId;
                    }
                    else
                    {
                        var contractor = await _context.SubContractors.FindAsync(assignment.AssigneeId);
                        if (contractor != null)
                        {
                            targetUserId = contractor.PortalUserId;
                            
                            if (!targetUserId.HasValue && !string.IsNullOrEmpty(contractor.Email))
                            {
                                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == contractor.Email);
                                if (user != null)
                                {
                                    targetUserId = user.Id;
                                    contractor.PortalUserId = user.Id;
                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation("Auto-linked SubContractor {Name} to User {User} via Email", contractor.Name, user.Id);
                                }
                            }
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
