using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.API.Security;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for managing project tasks, status rollups, subcontractor assignments, and contractor performance tracking.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProjectTasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<ProjectTasksController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectTasksController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="hubContext">SignalR notification hub context.</param>
        /// <param name="logger">Logger instance.</param>
        public ProjectTasksController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<ProjectTasksController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves project tasks, with support for project filtering, pagination, and user assignment filtering.
        /// </summary>
        /// <param name="projectId">Optional project ID filter.</param>
        /// <param name="assignedToMe">If true, filters tasks assigned to or managed by the authenticated user.</param>
        /// <param name="skip">Number of records to skip for pagination.</param>
        /// <param name="take">Number of records to return.</param>
        /// <returns>A list of project tasks.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectTask>>> GetProjectTasks(Guid? projectId = null, bool assignedToMe = false, int skip = 0, int take = 1000)
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
                    if (User.IsInRole("Admin"))
                    {
                        _logger.LogInformation("Admin user bypasses assignment filter.");
                        if (projectId.HasValue)
                        {
                            query = query.Where(t => t.ProjectId == projectId.Value);
                        }
                    }
                    else
                    {
                        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                                         ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                        
                        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId)) 
                        {
                            _logger.LogWarning("AssignedToMe requested but User ID not found in claims.");
                            return Unauthorized();
                        }

                        _logger.LogInformation("Filtering tasks for User ID: {UserId}", userId);

                        var linkedEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.LinkedUserId == userId);
                        
                        if (linkedEmployee == null)
                        {
                            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                            if (!string.IsNullOrEmpty(userEmail))
                            {
                                linkedEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == userEmail);
                                if (linkedEmployee != null)
                                {
                                    linkedEmployee.LinkedUserId = userId;
                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation("Auto-linked User {Email} to Employee {Id}", userEmail, linkedEmployee.Id);
                                }
                            }
                        }

                        if (linkedEmployee != null)
                            _logger.LogInformation("User is linked to Employee: {EmployeeId}", linkedEmployee.Id);
                        else
                            _logger.LogWarning("User {UserId} is NOT linked to any Employee record.", userId);

                        var teamIds = linkedEmployee != null 
                            ? await _context.TeamMembers.AsNoTracking()
                                .Where(tm => tm.EmployeeId == linkedEmployee.Id)
                                .Select(tm => tm.TeamId)
                                .ToListAsync() 
                            : new List<Guid>();

                        query = query.Where(t => 
                            ((t.OwnerId == userId) || 
                             (t.Assignments.Any(a => 
                                (a.AssigneeType == AssigneeType.Staff && linkedEmployee != null && a.AssigneeId == linkedEmployee.Id) ||
                                (a.AssigneeType == AssigneeType.Contractor && a.AssigneeId == userId) ||
                                (a.AssigneeType == AssigneeType.Team && teamIds.Contains(a.AssigneeId))
                             )) ||
                             (linkedEmployee != null && t.Project != null && t.Project.SiteManagerId == linkedEmployee.Id))
                        );

                        if (projectId.HasValue)
                        {
                            query = query.Where(t => t.ProjectId == projectId.Value);
                        }
                    }
                    
                    _logger.LogInformation("Query constructed for AssignedToMe for Project: {ProjectId}", projectId);
                }

                var results = await query
                    .OrderBy(t => t.OrderIndex)
                    .Skip(Math.Max(0, skip))
                    .Take(Math.Clamp(take, 1, 1000))
                    .ToListAsync();
                
                _logger.LogInformation("Returning {Count} tasks.", results.Count);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tasks");
                return StatusCode(500, "An error occurred while retrieving project tasks.");
            }
        }

        /// <summary>
        /// Retrieves tasks assigned to a specific subcontractor.
        /// </summary>
        /// <param name="subContractorId">The subcontractor ID.</param>
        /// <returns>A list of tasks assigned to the subcontractor.</returns>
        [HttpGet("assigned-to/{subContractorId}")]
        public async Task<ActionResult<IEnumerable<ProjectTask>>> GetSubContractorTasks(Guid subContractorId)
        {
            if (subContractorId == Guid.Empty) return BadRequest("Invalid subcontractor ID.");

            try
            {
                var query = _context.ProjectTasks
                    .Include(t => t.Assignments)
                    .Where(t => t.Assignments.Any(a => a.AssigneeType == AssigneeType.Contractor && a.AssigneeId == subContractorId))
                    .AsNoTracking();

                return Ok(await query.ToListAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tasks for subcontractor {Id}", subContractorId);
                return StatusCode(500, "An error occurred while retrieving subcontractor tasks.");
            }
        }

        /// <summary>
        /// Retrieves a single project task by its ID.
        /// </summary>
        /// <param name="id">The task ID.</param>
        /// <returns>The requested project task.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectTask>> GetProjectTask(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid task ID.");

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
                return Ok(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving task {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the task.");
            }
        }

        /// <summary>
        /// Retrieves recent task status updates across active projects for dashboard presentation.
        /// </summary>
        /// <returns>A list of dashboard update DTOs.</returns>
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
                
                var projectIds = topTasks.Where(t => t.ProjectId.HasValue).Select(t => t.ProjectId!.Value).Distinct().ToList();
                var projectMap = await _context.Projects
                    .Where(p => projectIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Name);

                var dtos = topTasks.Select(t => 
                {
                    var userId = string.IsNullOrEmpty(t.UpdatedBy) ? t.CreatedBy : t.UpdatedBy;
                    string? displayName = null;
                    if (userId == "System") displayName = "System";
                    else if (!string.IsNullOrEmpty(userId)) userMap.TryGetValue(userId, out displayName);

                    string? pName = t.Project?.Name;
                    if (string.IsNullOrEmpty(pName) && t.ProjectId.HasValue && t.ProjectId.Value != Guid.Empty)
                    {
                        projectMap.TryGetValue(t.ProjectId.Value, out pName);
                    }

                    return new OCC.Shared.DTOs.DashboardUpdateDto
                    {
                        Timestamp = t.UpdatedAtUtc ?? t.CreatedAtUtc,
                        User = userId,
                        DisplayName = displayName,
                        Action = "Status Changed",
                        TaskName = t.Name,
                        ProjectName = pName ?? "Unknown Project",
                        ProjectId = t.ProjectId,
                        Status = t.Status
                    };
                }).ToList();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent task updates");
                return StatusCode(500, "An error occurred while retrieving recent updates.");
            }
        }

        /// <summary>
        /// Creates a new project task.
        /// </summary>
        /// <param name="task">The project task entity.</param>
        /// <returns>The created task entity.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<ActionResult<ProjectTask>> PostProjectTask(ProjectTask task)
        {
            if (task == null) return BadRequest("Task payload cannot be null.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                if (task.Id == Guid.Empty) task.Id = Guid.NewGuid();
                
                // Input sanitization
                task.Name = InputSanitizer.Sanitize(task.Name);
                task.Description = InputSanitizer.Sanitize(task.Description);
                task.HoldReason = InputSanitizer.Sanitize(task.HoldReason);
                task.AssignedTo = InputSanitizer.Sanitize(task.AssignedTo);
                task.PercentComplete = Math.Clamp(task.PercentComplete, 0, 100);

                TaskHelper.EnsureUtcDates(task);
                task.PlannedDurationHours ??= TimeSpan.Zero;

                _context.ProjectTasks.Add(task);
                await _context.SaveChangesAsync();

                if (task.ParentId.HasValue && task.ParentId.Value != Guid.Empty)
                {
                    await CalculateParentRollup(task.ParentId.Value);
                }

                var idStr = task.Id.ToString();
                _logger.LogInformation("[SignalR-Broadcast] Notifying all clients: ProjectTask Create {Id}", idStr);
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "ProjectTask", "Create", idStr);
                
                if (task.ProjectId != Guid.Empty)
                {
                    _logger.LogInformation("[SignalR-Broadcast] Notifying all clients: Project Update {Id}", task.ProjectId);
                    await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Update", task.ProjectId.ToString());
                }

                return CreatedAtAction("GetProjectTask", new { id = task.Id }, task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task");
                return StatusCode(500, "An error occurred while creating the task.");
            }
        }

        /// <summary>
        /// Updates an existing project task and performs parent progress rollup.
        /// </summary>
        /// <param name="id">The ID of the task to update.</param>
        /// <param name="task">The updated task entity.</param>
        /// <returns>No content on success.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> PutProjectTask(Guid id, ProjectTask task)
        {
            if (id == Guid.Empty || id != task.Id)
            {
                return BadRequest("Task ID mismatch or empty.");
            }
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existingTask = await _context.ProjectTasks
                .Include(t => t.Children)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (existingTask == null)
            {
                return NotFound();
            }

            var oldParentId = existingTask.ParentId;

            try
            {
                _logger.LogInformation("Updating ProjectTask {Id}. New Status: {Status}, %: {Percent}", id, task.Status, task.PercentComplete);

                existingTask.Name = InputSanitizer.Sanitize(task.Name);
                existingTask.Description = InputSanitizer.Sanitize(task.Description);
                existingTask.StartDate = TaskHelper.EnsureUtc(task.StartDate);
                existingTask.FinishDate = TaskHelper.EnsureUtc(task.FinishDate);
                existingTask.ActualStartDate = TaskHelper.EnsureUtc(task.ActualStartDate);
                existingTask.ActualCompleteDate = TaskHelper.EnsureUtc(task.ActualCompleteDate);
                existingTask.PercentComplete = Math.Clamp(task.PercentComplete, 0, 100);
                existingTask.Priority = task.Priority;
                
                var wasNotCompleted = existingTask.Status != "Completed" && existingTask.Status != "Done";
                existingTask.Status = task.Status;
                
                if (wasNotCompleted && (task.Status == "Completed" || task.Status == "Done"))
                {
                    existingTask.ActualCompleteDate = DateTime.UtcNow;
                    await UpdateContractorPerformance(existingTask);
                }
                else if (!wasNotCompleted && task.Status != "Completed" && task.Status != "Done")
                {
                    existingTask.ActualCompleteDate = null;
                }

                existingTask.Duration = task.Duration;
                existingTask.PlannedDurationHours = task.PlannedDurationHours;
                existingTask.ActualDuration = task.ActualDuration;
                existingTask.ProjectId = task.ProjectId;
                existingTask.ParentId = task.ParentId;
                existingTask.Type = task.Type;
                existingTask.IsOnHold = task.IsOnHold;
                existingTask.HoldReason = InputSanitizer.Sanitize(task.HoldReason);
                existingTask.OrderIndex = task.OrderIndex;
                existingTask.IndentLevel = task.IndentLevel;
                existingTask.IsGroup = task.IsGroup;
                existingTask.OwnerId = task.OwnerId; 
                existingTask.NextReminderDate = task.NextReminderDate;
                existingTask.IsReminderSet = task.IsReminderSet;
                existingTask.Frequency = task.Frequency;
                existingTask.AssignedTo = InputSanitizer.Sanitize(task.AssignedTo);
                existingTask.Predecessors = task.Predecessors ?? new List<string>();
                
                if (existingTask.Status == "Completed" || existingTask.PercentComplete == 100)
                {
                    await MarkChildrenCompleted(existingTask);
                }

                var updatedTaskIds = new List<Guid> { id };

                if (existingTask.ParentId.HasValue)
                {
                    await CalculateParentRollup(existingTask.ParentId.Value, updatedTaskIds);
                }

                if (oldParentId.HasValue && oldParentId.Value != existingTask.ParentId)
                {
                    await CalculateParentRollup(oldParentId.Value, updatedTaskIds);
                }

                if (existingTask.ProjectId.HasValue && existingTask.ProjectId.Value != Guid.Empty)
                {
                    await UpdateProjectStatusAsync(existingTask.ProjectId.Value);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully saved ProjectTask {Id}", id);

                try
                {
                    foreach (var updatedId in updatedTaskIds)
                    {
                        var idStr = updatedId.ToString();
                        _logger.LogInformation("[SignalR-Broadcast] Notifying all clients: ProjectTask Update {Id}", idStr);
                        await _hubContext.Clients.All.SendAsync("EntityUpdate", "ProjectTask", "Update", idStr);
                    }
                    
                    if (existingTask.ProjectId != Guid.Empty)
                    {
                        var projIdStr = existingTask.ProjectId.ToString();
                        _logger.LogInformation("[SignalR-Broadcast] Notifying all clients: Project Update {Id}", projIdStr);
                        await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Update", projIdStr);
                    }

                    if (existingTask.Status != task.Status)
                    {
                        var pName = await _context.Projects
                            .Where(p => p.Id == task.ProjectId)
                            .Select(p => p.Name)
                            .FirstOrDefaultAsync() ?? "Unknown Project";

                        var updateDto = new OCC.Shared.DTOs.DashboardUpdateDto
                        {
                            Timestamp = DateTime.UtcNow,
                            User = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "System",
                            DisplayName = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value,
                            TaskName = task.Name,
                            ProjectName = pName,
                            ProjectId = task.ProjectId,
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
                string errorMessage = ex.Message;
                if (ex.InnerException != null) errorMessage += " | Inner: " + ex.InnerException.Message;
                
                _logger.LogError(ex, "Update failed for ProjectTask {Id}: {Message}", id, errorMessage);

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
                
                return StatusCode(500, "An error occurred while updating the project task.");
            }
        }

        /// <summary>
        /// Deletes a project task by its unique identifier.
        /// </summary>
        /// <param name="id">The task ID.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> DeleteProjectTask(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid task ID.");

            try
            {
                var task = await _context.ProjectTasks.FindAsync(id);
                if (task == null) return NotFound();

                var parentId = task.ParentId;

                _context.ProjectTasks.Remove(task);
                await _context.SaveChangesAsync();

                if (parentId.HasValue && parentId.Value != Guid.Empty)
                {
                    await CalculateParentRollup(parentId.Value);
                }

                var idStr = id.ToString();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "ProjectTask", "Delete", idStr);
                
                if (task.ProjectId != Guid.Empty)
                {
                    await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Update", task.ProjectId.ToString());
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task {Id}", id);
                return StatusCode(500, "An error occurred while deleting the task.");
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
                
                if (child.IsGroup)
                {
                    await _context.Entry(child).Collection(c => c.Children).LoadAsync();
                    await MarkChildrenCompleted(child);
                }
            }
        }

        private async Task CalculateParentRollup(Guid parentId, List<Guid>? updatedTaskIds = null)
        {
            try
            {
                var parent = await _context.ProjectTasks
                    .Include(t => t.Children)
                    .FirstOrDefaultAsync(t => t.Id == parentId);

                if (parent != null)
                {
                    bool parentChanged = false;

                    if (parent.Children.Any())
                    {
                        if (!parent.IsGroup)
                        {
                            parent.IsGroup = true;
                            parentChanged = true;
                        }

                        var children = parent.Children.ToList();
                        double average = children.Average(c => (double)c.PercentComplete);
                        int rounded = (int)Math.Round(average);

                        if (parent.PercentComplete != rounded)
                        {
                            _logger.LogInformation("Rolling up progress for Parent {Id}: {Old}% -> {New}%", parentId, parent.PercentComplete, rounded);
                            
                            parent.PercentComplete = rounded;

                            if (rounded == 100) parent.Status = "Done";
                            else if (rounded > 0 && (parent.Status == "To Do" || parent.Status == "Not Started")) 
                                parent.Status = "Started";

                            parentChanged = true;
                        }
                    }
                    else
                    {
                        if (parent.IsGroup)
                        {
                            parent.IsGroup = false;
                            parentChanged = true;
                        }
                    }

                    if (parentChanged)
                    {
                        if (updatedTaskIds != null)
                        {
                            if (!updatedTaskIds.Contains(parent.Id))
                            {
                                updatedTaskIds.Add(parent.Id);
                            }

                            if (parent.ParentId.HasValue)
                            {
                                await CalculateParentRollup(parent.ParentId.Value, updatedTaskIds);
                            }
                        }
                        else
                        {
                            await _context.SaveChangesAsync();
                            
                            await _hubContext.Clients.All.SendAsync("EntityUpdate", "ProjectTask", "Update", parent.Id);

                            if (parent.ParentId.HasValue)
                            {
                                await CalculateParentRollup(parent.ParentId.Value);
                            }
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
                        
                        bool isOnTime = (task.ActualCompleteDate ?? DateTime.UtcNow) <= task.FinishDate;
                        
                        int oldCount = contractor.CompletedTasksCount - 1;
                        if (contractor.CompletedTasksCount > 0)
                        {
                            contractor.OnTimeRate = (contractor.OnTimeRate * oldCount + (isOnTime ? 1m : 0m)) / contractor.CompletedTasksCount;
                        }
                        
                        decimal baseRating = contractor.OnTimeRate * 5.0m;
                        
                        var snags = await _context.SnagJobs.Where(s => s.SubContractorId == contractor.Id).ToListAsync();
                        int activeSnags = snags.Count(s => s.Status == SnagStatus.Open || s.Status == SnagStatus.InProgress);
                        int resolvedSnags = snags.Count - activeSnags;
                        
                        decimal activeDeduction = activeSnags * 0.3m;
                        decimal snagRatio = contractor.CompletedTasksCount > 0 
                            ? (decimal)resolvedSnags / contractor.CompletedTasksCount 
                            : resolvedSnags > 0 ? 0.5m : 0m;
                        
                        decimal historicalDeduction = Math.Min(snagRatio * 1.5m, 1.5m);
                        
                        contractor.Rating = Math.Max(1.0m, Math.Min(5.0m, baseRating - activeDeduction - historicalDeduction));

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

        private async Task UpdateProjectStatusAsync(Guid projectId)
        {
            try
            {
                var taskData = await _context.ProjectTasks
                    .Where(t => t.ProjectId == projectId && t.IsActive)
                    .Select(t => new { t.PercentComplete, t.FinishDate })
                    .ToListAsync();

                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

                if (project != null && taskData.Any())
                {
                    double averageProgress = taskData.Average(t => (double)t.PercentComplete);
                    var roundedProgress = Math.Round(averageProgress);
                    var dbStatus = _context.Entry(project).OriginalValues.GetValue<string>("Status") ?? "Planning";
                    var oldEndDate = project.EndDate;

                    string newStatus = dbStatus;
                    if (roundedProgress >= 100 && dbStatus != "Archived" && dbStatus != "OnHold" && dbStatus != "Cancelled")
                    {
                        newStatus = "Completed";
                    }
                    else if (roundedProgress > 0 && roundedProgress < 100 && (dbStatus == "Planning" || dbStatus == "Not Started" || dbStatus == "Completed" || dbStatus == "Active"))
                    {
                        newStatus = "In Progress";
                    }
                    else if (roundedProgress == 0 && dbStatus == "Completed")
                    {
                        newStatus = "Not Started";
                    }

                    var maxFinishDate = taskData.Max(t => t.FinishDate);
                    var newEndDate = oldEndDate;
                    if (maxFinishDate > oldEndDate)
                    {
                        newEndDate = maxFinishDate;
                    }

                    if (dbStatus != newStatus || oldEndDate != newEndDate)
                    {
                        project.Status = newStatus;
                        project.EndDate = newEndDate;
                        _logger.LogInformation("Project {Id} updated. Status: {OldStatus}->{NewStatus}, EndDate: {OldEndDate}->{NewEndDate}", 
                            project.Id, dbStatus, newStatus, oldEndDate, newEndDate);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project status and end date for {ProjectId}", projectId);
            }
        }
    }
    
    /// <summary>
    /// Utility helper for date standardization in tasks.
    /// </summary>
    public static class TaskHelper
    {
        /// <summary>
        /// Ensures a DateTime instance is specified as UTC.
        /// </summary>
        public static DateTime EnsureUtc(DateTime date)
        {
            if (date.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(date, DateTimeKind.Utc);
            return date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : date;
        }

        /// <summary>
        /// Ensures a nullable DateTime instance is specified as UTC.
        /// </summary>
        public static DateTime? EnsureUtc(DateTime? date)
        {
            if (!date.HasValue) return null;
            if (date.Value.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(date.Value, DateTimeKind.Utc);
            return date.Value.Kind == DateTimeKind.Local ? date.Value.ToUniversalTime() : date.Value;
        }

        /// <summary>
        /// Converts all date fields of a task to UTC.
        /// </summary>
        public static void EnsureUtcDates(ProjectTask task)
        {
            task.StartDate = EnsureUtc(task.StartDate);
            task.FinishDate = EnsureUtc(task.FinishDate);
            task.ActualStartDate = EnsureUtc(task.ActualStartDate);
            task.ActualCompleteDate = EnsureUtc(task.ActualCompleteDate);
        }
    }
}
