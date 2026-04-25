using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProjectTasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<ProjectTasksController> _logger;

        public ProjectTasksController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<ProjectTasksController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/ProjectTasks
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectTask>>> GetProjectTasks(Guid? projectId = null, bool assignedToMe = false)
        {
            try
            {
                var query = _context.ProjectTasks
                    .Include(t => t.Project)
                    .Include(t => t.Assignments)
                    .Include(t => t.Comments)
                    .Include(t => t.Children)
                    .AsNoTracking()
                    .AsQueryable();

                if (projectId.HasValue)
                {
                    query = query.Where(t => t.ProjectId == projectId.Value);
                }

                if (assignedToMe)
                {
                    // 1. Get current logged-in user's ID
                    var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                    if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId)) 
                        return Unauthorized();

                    // 2. Find the linked Employee (if any)
                    var linkedEmployee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.LinkedUserId == userId);

                    // 3. Get Team IDs if user is an employee
                    var teamIds = linkedEmployee != null 
                        ? await _context.TeamMembers.AsNoTracking()
                            .Where(tm => tm.EmployeeId == linkedEmployee.Id)
                            .Select(tm => tm.TeamId)
                            .ToListAsync() 
                        : new List<Guid>();

                    // 4. Update the query to find any assignment matching the user's identities OR tasks owned by the user
                    query = query.Where(t => 
                        (t.OwnerId == userId) || 
                        (t.Assignments.Any(a => 
                            (a.AssigneeType == AssigneeType.Staff && linkedEmployee != null && a.AssigneeId == linkedEmployee.Id) ||
                            (a.AssigneeType == AssigneeType.Contractor && a.AssigneeId == userId) ||
                            (a.AssigneeType == AssigneeType.Team && teamIds.Contains(a.AssigneeId))
                        ))
                    );
                }

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tasks");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("assigned-to/{subContractorId}")]
        public async Task<ActionResult<IEnumerable<ProjectTask>>> GetSubContractorTasks(Guid subContractorId)
        {
            try
            {
                var query = _context.ProjectTasks
                    .Include(t => t.Assignments)
                    .Where(t => t.Assignments.Any(a => a.AssigneeType == AssigneeType.Contractor && a.AssigneeId == subContractorId))
                    .AsNoTracking();

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tasks for subcontractor {Id}", subContractorId);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/ProjectTasks/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectTask>> GetProjectTask(Guid id)
        {
            try
            {
                var task = await _context.ProjectTasks
                    .Include(t => t.Project)
                    .Include(t => t.Assignments)
                    .Include(t => t.Comments)
                    .Include(t => t.Children)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (task == null) return NotFound();
                return task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving task {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("recent-updates")]
        public async Task<ActionResult<IEnumerable<OCC.Shared.DTOs.DashboardUpdateDto>>> GetRecentUpdates()
        {
            try
            {
                var explicitStatuses = new[] { "Started", "Halfway", "Almost Done", "Completed", "Done" };
                
                var topTasks = await _context.ProjectTasks
                    .Include(t => t.Project)
                    .Where(t => explicitStatuses.Contains(t.Status))
                    .OrderByDescending(t => t.UpdatedAtUtc ?? t.CreatedAtUtc)
                    .Take(10)
                    .AsNoTracking()
                    .ToListAsync();

                // Resolve Display Names
                var userIds = topTasks.Select(t => string.IsNullOrEmpty(t.UpdatedBy) ? t.CreatedBy : t.UpdatedBy)
                    .Where(id => !string.IsNullOrEmpty(id) && id != "System")
                    .Distinct()
                    .ToList();

                var userGuids = userIds.Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .ToList();

                var userMap = await _context.Users
                    .Where(u => userGuids.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id.ToString(), u => u.DisplayName ?? u.Email);
                
                var dtos = topTasks.Select(t => 
                {
                    var userId = string.IsNullOrEmpty(t.UpdatedBy) ? t.CreatedBy : t.UpdatedBy;
                    string? displayName = null;
                    if (userId == "System") displayName = "System";
                    else if (!string.IsNullOrEmpty(userId)) userMap.TryGetValue(userId, out displayName);

                    return new OCC.Shared.DTOs.DashboardUpdateDto
                    {
                        Timestamp = t.UpdatedAtUtc ?? t.CreatedAtUtc,
                        User = userId,
                        DisplayName = displayName,
                        Action = "Status Changed",
                        TaskName = t.Name,
                        ProjectName = t.Project?.Name ?? string.Empty,
                        Status = t.Status
                    };
                }).ToList();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent task updates");
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/ProjectTasks
        [HttpPost]
        public async Task<ActionResult<ProjectTask>> PostProjectTask(ProjectTask task)
        {
            try
            {
                if (task.Id == Guid.Empty) task.Id = Guid.NewGuid();
                TaskHelper.EnsureUtcDates(task);

                // Legacy column protection
                task.PlannedDurationHours ??= TimeSpan.Zero;

                _context.ProjectTasks.Add(task);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("EntityUpdate", "ProjectTask", "Create", task.Id);

                return CreatedAtAction("GetProjectTask", new { id = task.Id }, task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/ProjectTasks/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutProjectTask(Guid id, ProjectTask task)
        {
            if (id != task.Id)
            {
                return BadRequest();
            }

            var existingTask = await _context.ProjectTasks
                .Include(t => t.Children)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (existingTask == null)
            {
                return NotFound();
            }

            try
            {
                // DEBUG LOGGING TO AuditLog TABLE
                var logEntry = new AuditLog
                {
                    UserId = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "System",
                    TableName = "ProjectTasks",
                    RecordId = id.ToString(),
                    Action = "Update Start",
                    Timestamp = DateTime.UtcNow,
                    NewValues = $"Task: {task.Name} | %: {task.PercentComplete} | Status: {task.Status} | Start: {task.StartDate:u} | End: {task.FinishDate:u}"
                };
                _context.AuditLogs.Add(logEntry);
                await _context.SaveChangesAsync();

                // Surgical Update: Copy scalar properties only to avoid EF navigation issues
                existingTask.Name = task.Name;
                existingTask.Description = task.Description;
                existingTask.StartDate = TaskHelper.EnsureUtc(task.StartDate);
                existingTask.FinishDate = TaskHelper.EnsureUtc(task.FinishDate);
                existingTask.ActualStartDate = TaskHelper.EnsureUtc(task.ActualStartDate);
                existingTask.ActualCompleteDate = TaskHelper.EnsureUtc(task.ActualCompleteDate);
                existingTask.PercentComplete = task.PercentComplete;
                existingTask.Priority = task.Priority;
                
                var wasNotCompleted = existingTask.Status != "Completed" && existingTask.Status != "Done";
                existingTask.Status = task.Status;
                
                if (wasNotCompleted && (task.Status == "Completed" || task.Status == "Done"))
                {
                    await UpdateContractorPerformance(existingTask);
                }

                existingTask.Duration = task.Duration;
                existingTask.PlannedDurationHours = task.PlannedDurationHours;
                existingTask.ActualDuration = task.ActualDuration;
                existingTask.ProjectId = task.ProjectId;
                existingTask.ParentId = task.ParentId;
                existingTask.Type = task.Type;
                existingTask.IsOnHold = task.IsOnHold;
                existingTask.HoldReason = task.HoldReason;
                existingTask.OrderIndex = task.OrderIndex;
                existingTask.IndentLevel = task.IndentLevel;
                existingTask.IsGroup = task.IsGroup;
                existingTask.OwnerId = task.OwnerId; 
                existingTask.NextReminderDate = task.NextReminderDate;
                existingTask.IsReminderSet = task.IsReminderSet;
                existingTask.Frequency = task.Frequency;
                
                // Recursive Completion: If a parent is marked Completed, mark all children as Completed.
                // This preserves DB integrity where parents can't be done if kids are active.
                if (existingTask.Status == "Completed" || existingTask.PercentComplete == 100)
                {
                    await MarkChildrenCompleted(existingTask);
                }

                // Signal automated project status if progress starts
                await _context.SaveChangesAsync();

                // 2. Perform Rollup: Update parent task progress based on child changes
                if (existingTask.ParentId.HasValue)
                {
                    await CalculateParentRollup(existingTask.ParentId.Value);
                }

                await _context.SaveChangesAsync();

                // Explicitly save Success Log
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = "System",
                    TableName = "ProjectTasks",
                    RecordId = id.ToString(),
                    Action = "Update Success",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                // Wrap SignalR in try-catch to avoid 500 if broadcast fails
                try
                {
                    await _hubContext.Clients.All.SendAsync("EntityUpdate", "ProjectTask", "Update", id);
                    
                    if (existingTask.Status != task.Status)
                    {
                        var updateDto = new OCC.Shared.DTOs.DashboardUpdateDto
                        {
                            Timestamp = DateTime.UtcNow,
                            User = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "System",
                            DisplayName = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value,
                            TaskName = task.Name,
                            Status = task.Status
                        };
                        await _hubContext.Clients.All.SendAsync("DashboardUpdate", updateDto);
                    }
                }
                catch (Exception sigEx)
                {
                    _logger.LogWarning(sigEx, "SignalR broadcast failed for Task {Id}", id);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                // Detailed logging for troubleshooting
                string errorMessage = ex.Message;
                if (ex.InnerException != null) errorMessage += " | Inner: " + ex.InnerException.Message;
                
                _logger.LogError(ex, "Update failed for ProjectTask {Id}: {Message}", id, errorMessage);

                // Use a fresh scope to save the error log, as the current _context might be "poisoned" 
                // and would try to re-save the failed entity along with the log.
                try
                {
                    using (var scope = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>().CreateScope())
                    {
                        var freshContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        freshContext.AuditLogs.Add(new AuditLog
                        {
                            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "System",
                            TableName = "ProjectTasks",
                            RecordId = id.ToString(),
                            Action = "Update Error",
                            Timestamp = DateTime.UtcNow,
                            NewValues = $"Error: {errorMessage} | Stack: {ex.StackTrace?.Substring(0, Math.Min(ex.StackTrace.Length, 1000))}"
                        });
                        await freshContext.SaveChangesAsync();
                    }
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "FATAL: Could not even log the update error to the database via fresh context.");
                }
                
                return StatusCode(500, $"Internal server error: {errorMessage}");
            }
        }

        // DELETE: api/ProjectTasks/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProjectTask(Guid id)
        {
            try
            {
                var task = await _context.ProjectTasks.FindAsync(id);
                if (task == null) return NotFound();
                _context.ProjectTasks.Remove(task);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("EntityUpdate", "ProjectTask", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
        
        private async Task MarkChildrenCompleted(ProjectTask parent)
        {
            if (parent.Children == null || !parent.Children.Any()) return;

            foreach (var child in parent.Children)
            {
                child.Status = "Completed";
                child.PercentComplete = 100;
                child.ActualCompleteDate = TaskHelper.EnsureUtc(DateTime.UtcNow);
                
                // Recursively load and mark grandchildren if this is a group
                if (child.IsGroup)
                {
                    // Ensure children are loaded for the recursive call
                    await _context.Entry(child).Collection(c => c.Children).LoadAsync();
                    await MarkChildrenCompleted(child);
                }
            }
        }

        private async Task CalculateParentRollup(Guid parentId)
        {
            try
            {
                var parent = await _context.ProjectTasks
                    .Include(t => t.Children)
                    .FirstOrDefaultAsync(t => t.Id == parentId);

                if (parent != null && parent.Children.Any())
                {
                    // Compute average progress of children
                    // We only rollup progress for non-meeting tasks or handle based on business rules
                    var children = parent.Children.ToList();
                    double average = children.Average(c => (double)c.PercentComplete);
                    int rounded = (int)Math.Round(average);

                    if (parent.PercentComplete != rounded)
                    {
                        _logger.LogInformation("Rolling up progress for Parent {Id}: {Old}% -> {New}%", parentId, parent.PercentComplete, rounded);
                        
                        parent.PercentComplete = rounded;

                        // Sync Status if progress is 100% or just started
                        if (rounded == 100) parent.Status = "Done";
                        else if (rounded > 0 && (parent.Status == "To Do" || parent.Status == "Not Started")) 
                            parent.Status = "Started";

                        await _context.SaveChangesAsync();
                        
                        // Notify clients about parent update
                        await _hubContext.Clients.All.SendAsync("EntityUpdate", "ProjectTask", "Update", parent.Id);

                        // Recurse up the tree
                        if (parent.ParentId.HasValue)
                        {
                            await CalculateParentRollup(parent.ParentId.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CalculateParentRollup for {ParentId}", parentId);
            }
        }

        private bool ProjectTaskExists(Guid id) => _context.ProjectTasks.Any(e => e.Id == id);

        private async Task UpdateContractorPerformance(ProjectTask task)
        {
            try
            {
                // Ensure assignments are loaded
                await _context.Entry(task).Collection(t => t.Assignments).LoadAsync();
                
                var contractorAssignments = task.Assignments
                    .Where(a => a.AssigneeType == AssigneeType.Contractor)
                    .ToList();

                foreach (var assignment in contractorAssignments)
                {
                    var contractor = await _context.SubContractors.FindAsync(assignment.AssigneeId);
                    if (contractor != null)
                    {
                        contractor.CompletedTasksCount++;
                        
                        // On-time check
                        // Note: Ensure dates are compared correctly as UTC
                        bool isOnTime = (task.ActualCompleteDate ?? DateTime.UtcNow) <= task.FinishDate;
                        
                        // Calculate new OnTimeRate
                        int oldCount = contractor.CompletedTasksCount - 1;
                        if (contractor.CompletedTasksCount > 0)
                        {
                            contractor.OnTimeRate = (contractor.OnTimeRate * oldCount + (isOnTime ? 1m : 0m)) / contractor.CompletedTasksCount;
                        }
                        
                        // Recalculate Rating using the Dilution Formula
                        decimal baseRating = contractor.OnTimeRate * 5.0m;
                        
                        // Get snag stats for fair weighting
                        var snags = await _context.SnagJobs.Where(s => s.SubContractorId == contractor.Id).ToListAsync();
                        int activeSnags = snags.Count(s => s.Status == SnagStatus.Open || s.Status == SnagStatus.InProgress);
                        int resolvedSnags = snags.Count - activeSnags;
                        
                        decimal activeDeduction = activeSnags * 0.3m;
                        decimal snagRatio = contractor.CompletedTasksCount > 0 
                            ? (decimal)resolvedSnags / contractor.CompletedTasksCount 
                            : resolvedSnags > 0 ? 0.5m : 0m;
                        
                        decimal historicalDeduction = Math.Min(snagRatio * 1.5m, 1.5m);
                        
                        contractor.Rating = Math.Max(1.0m, Math.Min(5.0m, baseRating - activeDeduction - historicalDeduction));

                        // Set Tier
                        contractor.PerformanceTier = contractor.Rating switch
                        {
                            >= 4.8m => "Diamond",
                            >= 4.0m => "Gold",
                            >= 3.0m => "Silver",
                            _ => "Bronze"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contractor performance for task {Id}", task.Id);
            }
        }
    }
    
    public static class TaskHelper
    {
        public static DateTime EnsureUtc(DateTime date)
        {
            if (date.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(date, DateTimeKind.Utc);
            return date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : date;
        }

        public static DateTime? EnsureUtc(DateTime? date)
        {
            if (!date.HasValue) return null;
            if (date.Value.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(date.Value, DateTimeKind.Utc);
            return date.Value.Kind == DateTimeKind.Local ? date.Value.ToUniversalTime() : date.Value;
        }

        public static void EnsureUtcDates(ProjectTask task)
        {
            task.StartDate = EnsureUtc(task.StartDate);
            task.FinishDate = EnsureUtc(task.FinishDate);
            task.ActualStartDate = EnsureUtc(task.ActualStartDate);
            task.ActualCompleteDate = EnsureUtc(task.ActualCompleteDate);
        }
    }
}
