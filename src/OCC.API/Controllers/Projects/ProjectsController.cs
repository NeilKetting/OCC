using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.API.Security;
using OCC.Shared.DTOs;
using OCC.Shared.Framework;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for managing construction projects, project summaries, personnel allocations, task imports, and lifecycle management.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<ProjectsController> _logger;
        private readonly Services.INotificationService _notificationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectsController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="hubContext">SignalR notification hub context.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="notificationService">Notification service.</param>
        public ProjectsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<ProjectsController> logger, Services.INotificationService notificationService)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Retrieves project summaries including calculated task progress, status rollups, and site manager details.
        /// </summary>
        /// <param name="includeDeleted">Whether to include soft-deleted projects.</param>
        /// <returns>A list of project summary DTOs.</returns>
        [HttpGet("summaries")]
        public async Task<ActionResult<IEnumerable<ProjectSummaryDto>>> GetProjectSummaries([FromQuery] bool includeDeleted = false)
        {
            try
            {
                var query = _context.Projects.AsQueryable();
                
                if (includeDeleted)
                {
                    query = query.IgnoreQueryFilters();
                }

                query = query
                    .Include(p => p.Tasks)
                    .Include(p => p.SiteManager);

                var projects = await query.ToListAsync();
                bool anyChanges = false;
                foreach (var p in projects)
                {
                    var avgProgress = p.Tasks.Any() ? p.Tasks.Average(t => (double)t.PercentComplete) : 0;
                    var oldStatus = p.Status;

                    if (Math.Round(avgProgress) >= 100 && p.Status != "Archived" && p.Status != "OnHold" && p.Status != "Cancelled")
                        p.Status = "Completed";
                    else if (avgProgress > 0 && (p.Status == "Planning" || p.Status == "Not Started"))
                        p.Status = "In Progress";
                    
                    if (oldStatus != p.Status)
                    {
                        anyChanges = true;
                        _logger.LogInformation("Updating DB Status for Project {Name} from {Old} to {New}", p.Name, oldStatus, p.Status);
                    }
                }
                if (anyChanges) await _context.SaveChangesAsync();

                var creators = projects.Select(p => p.CreatedBy).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
                var creatorGuids = creators.Select(c => Guid.TryParse(c, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
                var userMap = await _context.Users
                    .Where(u => creatorGuids.Contains(u.Id) || creators.Contains(u.Email))
                    .ToListAsync();
                
                var nameMap = new Dictionary<string, string>();
                foreach (var creator in creators)
                {
                    var user = userMap.FirstOrDefault(u => u.Id.ToString() == creator || u.Email == creator);
                    if (user != null) nameMap[creator] = user.DisplayName ?? user.Email ?? creator;
                    else nameMap[creator] = creator;
                }
 
                var summaries = projects.Select(p => 
                {
                    var avgProgress = p.Tasks.Any() ? p.Tasks.Average(t => (double)t.PercentComplete) : 0;
                    var displayStatus = p.Status;

                    if (Math.Round(avgProgress) >= 100 && p.Status != "Archived" && p.Status != "OnHold" && p.Status != "Cancelled")
                        displayStatus = "Completed";
                    else if (avgProgress > 0 && (p.Status == "Planning" || p.Status == "Not Started"))
                        displayStatus = "In Progress";

                    var projectManager = nameMap.GetValueOrDefault(p.CreatedBy, p.ProjectManager);

                    return new ProjectSummaryDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Status = displayStatus,
                        ProjectManager = projectManager,
                        TaskCount = p.Tasks.Count,
                        Progress = (int)Math.Round(avgProgress),
                        LatestFinish = p.Tasks.Any() ? p.Tasks.Max(t => t.FinishDate) : p.EndDate,
                        StartDate = p.StartDate,
                        SiteManagerId = p.SiteManagerId,
                        SiteManagerName = p.SiteManager?.DisplayName ?? "Unassigned",
                        IsActive = p.IsActive,
                        Priority = p.Priority
                    };
                }).ToList();

                return Ok(summaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project summaries");
                var msg = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
                return StatusCode(500, $"An error occurred while retrieving project summaries: {msg}");
            }
        }

        /// <summary>
        /// Retrieves paginated project summaries using OCC Enterprise Framework standards.
        /// </summary>
        [HttpGet("paged")]
        public async Task<ActionResult<ApiResponse<PagedResult<ProjectSummaryDto>>>> GetProjectsPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            try
            {
                var query = _context.Projects.AsNoTracking().Include(p => p.Tasks).Include(p => p.SiteManager).AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim().ToLower();
                    query = query.Where(p => p.Name.ToLower().Contains(term) || (p.Code != null && p.Code.ToLower().Contains(term)));
                }

                var totalCount = await query.CountAsync();
                var projects = await query
                    .OrderByDescending(p => p.CreatedAtUtc)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var summaries = projects.Select(p =>
                {
                    var avgProgress = p.Tasks.Any() ? (int)Math.Round(p.Tasks.Average(t => (double)t.PercentComplete)) : 0;
                    return new ProjectSummaryDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Status = p.Status,
                        SiteManagerId = p.SiteManagerId,
                        SiteManagerName = p.SiteManager != null ? $"{p.SiteManager.FirstName} {p.SiteManager.LastName}".Trim() : string.Empty,
                        Progress = avgProgress,
                        StartDate = p.StartDate,
                        TaskCount = p.Tasks.Count,
                        IsActive = p.IsActive,
                        Priority = p.Priority
                    };
                }).ToList();

                var pagedResult = PagedResult<ProjectSummaryDto>.Create(summaries, totalCount, page, pageSize);
                return Ok(ApiResponse<PagedResult<ProjectSummaryDto>>.Ok(pagedResult, "Projects retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated project summaries");
                return StatusCode(500, ApiResponse<PagedResult<ProjectSummaryDto>>.Fail("An internal server error occurred while retrieving project summaries."));
            }
        }

        /// <summary>
        /// Retrieves all projects, optionally filtered by current user assignment.
        /// </summary>
        /// <param name="assignedToMe">If true, filters projects assigned to the current user or managed by them.</param>
        /// <returns>A list of projects matching criteria.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Project>>> GetProjects(bool assignedToMe = false)
        {
            try
            {
                var query = _context.Projects
                    .Include(p => p.Tasks)
                    .ThenInclude(t => t.Assignments)
                    .Include(p => p.SiteManager)
                    .AsNoTracking()
                    .AsQueryable();

                if (assignedToMe)
                {
                    var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                    if (userIdClaim == null) return Unauthorized();
                    
                    if (!Guid.TryParse(userIdClaim.Value, out var userId))
                        return Unauthorized();

                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                    if (user == null) return Unauthorized();

                    if (user.UserRole == UserRole.Admin)
                    {
                        return Ok(await query.ToListAsync());
                    }

                    var linkedEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.LinkedUserId == user.Id);
                    
                    if (linkedEmployee == null && !string.IsNullOrEmpty(user.Email))
                    {
                        linkedEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
                        if (linkedEmployee != null)
                        {
                            linkedEmployee.LinkedUserId = user.Id;
                            await _context.SaveChangesAsync();
                        }
                    }

                    query = query.Where(p => 
                        (linkedEmployee != null && p.SiteManagerId == linkedEmployee.Id) || 
                        p.Tasks.Any(t => t.Assignments.Any(a => 
                            (a.AssigneeType == AssigneeType.Staff && linkedEmployee != null && a.AssigneeId == linkedEmployee.Id) ||
                            (a.AssigneeType == AssigneeType.Contractor && a.AssigneeId == userId)
                        ))
                    );
                }

                var projects = await query.ToListAsync();
                
                var creators = projects.Select(p => p.CreatedBy).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
                var creatorGuids = creators.Select(c => Guid.TryParse(c, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
                var userMap = await _context.Users
                    .Where(u => creatorGuids.Contains(u.Id) || creators.Contains(u.Email))
                    .ToListAsync();
 
                foreach (var p in projects)
                {
                    var avgProgress = p.Tasks.Any() ? p.Tasks.Average(t => (double)t.PercentComplete) : 0;
                    if (Math.Round(avgProgress) >= 100 && p.Status != "Archived" && p.Status != "OnHold" && p.Status != "Cancelled")
                        p.Status = "Completed";
                    else if (avgProgress > 0 && (p.Status == "Planning" || p.Status == "Not Started"))
                        p.Status = "In Progress";
                    
                    var user = userMap.FirstOrDefault(u => u.Id.ToString() == p.CreatedBy || u.Email == p.CreatedBy);
                    if (user != null)
                    {
                        p.ProjectManager = user.DisplayName ?? user.Email ?? p.CreatedBy;
                    }
                }
                return Ok(projects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects");
                return StatusCode(500, "An error occurred while retrieving projects.");
            }
        }

        /// <summary>
        /// Retrieves details for a single project by its unique identifier.
        /// </summary>
        /// <param name="id">The project ID.</param>
        /// <returns>The requested project entity.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Project>> GetProject(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid project ID.");

            try
            {
                var project = await _context.Projects
                    .Include(p => p.Tasks)
                    .ThenInclude(t => t.Assignments)
                    .Include(p => p.Tasks)
                    .ThenInclude(t => t.Comments)
                    .Include(p => p.SiteManager)
                    .Include(p => p.CustomerEntity)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (project == null) return NotFound();

                var avgProgress = project.Tasks.Any() ? project.Tasks.Average(t => (double)t.PercentComplete) : 0;
                if (Math.Round(avgProgress) >= 100 && project.Status != "Archived" && project.Status != "OnHold" && project.Status != "Cancelled")
                    project.Status = "Completed";
                else if (avgProgress > 0 && (project.Status == "Planning" || project.Status == "Not Started"))
                    project.Status = "In Progress";

                Guid.TryParse(project.CreatedBy, out var creatorGuid);
                var creator = await _context.Users.FirstOrDefaultAsync(u => (creatorGuid != Guid.Empty && u.Id == creatorGuid) || u.Email == project.CreatedBy);
                if (creator != null)
                {
                    project.ProjectManager = creator.DisplayName ?? creator.Email ?? project.CreatedBy;
                }
                else
                {
                    project.ProjectManager = project.CreatedBy;
                }

                return Ok(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the project.");
            }
        }

        /// <summary>
        /// Creates a new project entity.
        /// </summary>
        /// <param name="project">The project to create.</param>
        /// <returns>The created project entity.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<ActionResult<Project>> PostProject(Project project)
        {
            if (project == null) return BadRequest("Project payload cannot be null.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                if (project.Id == Guid.Empty) project.Id = Guid.NewGuid();
                
                project.Name = InputSanitizer.Sanitize(project.Name);
                project.Description = InputSanitizer.Sanitize(project.Description);
                project.Location = InputSanitizer.Sanitize(project.Location);
                project.Customer = InputSanitizer.Sanitize(project.Customer);
                project.Priority = InputSanitizer.Sanitize(project.Priority);
                project.ShortName = InputSanitizer.Sanitize(project.ShortName);
                project.StreetLine1 = InputSanitizer.Sanitize(project.StreetLine1);
                project.StreetLine2 = InputSanitizer.Sanitize(project.StreetLine2);
                project.City = InputSanitizer.Sanitize(project.City);
                project.StateOrProvince = InputSanitizer.Sanitize(project.StateOrProvince);
                project.PostalCode = InputSanitizer.Sanitize(project.PostalCode);
                project.Country = InputSanitizer.Sanitize(project.Country);

                var userEmail = User.Identity?.Name;
                if (!string.IsNullOrEmpty(userEmail))
                {
                    Guid.TryParse(userEmail, out var userGuid);
                    var user = await _context.Users.FirstOrDefaultAsync(u => (userGuid != Guid.Empty && u.Id == userGuid) || u.Email == userEmail);
                    project.ProjectManager = user?.DisplayName ?? user?.Email ?? userEmail;
                }

                project.SiteManager = null;
                project.CustomerEntity = null;

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Create", project.Id);

                if (project.SiteManagerId.HasValue)
                {
                    NotificationsController.LogPush($"[NOTIFY] PostProject triggered site manager assignment notification for manager: {project.SiteManagerId.Value}");
                    await NotifySiteManagerAssignmentAsync(project, project.SiteManagerId);
                }

                return CreatedAtAction("GetProject", new { id = project.Id }, project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project");
                return StatusCode(500, "An error occurred while creating the project.");
            }
        }

        /// <summary>
        /// Updates an existing project entity.
        /// </summary>
        /// <param name="id">The ID of the project to update.</param>
        /// <param name="project">The updated project entity.</param>
        /// <returns>No content on success.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> PutProject(Guid id, Project project)
        {
            if (id == Guid.Empty || id != project.Id) return BadRequest("Project ID mismatch or empty.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existingProject = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (existingProject == null)
            {
                return NotFound();
            }

            project.Name = InputSanitizer.Sanitize(project.Name);
            project.Description = InputSanitizer.Sanitize(project.Description);
            project.Location = InputSanitizer.Sanitize(project.Location);
            project.Customer = InputSanitizer.Sanitize(project.Customer);
            project.Priority = InputSanitizer.Sanitize(project.Priority);
            project.ShortName = InputSanitizer.Sanitize(project.ShortName);
            project.StreetLine1 = InputSanitizer.Sanitize(project.StreetLine1);
            project.StreetLine2 = InputSanitizer.Sanitize(project.StreetLine2);
            project.City = InputSanitizer.Sanitize(project.City);
            project.StateOrProvince = InputSanitizer.Sanitize(project.StateOrProvince);
            project.PostalCode = InputSanitizer.Sanitize(project.PostalCode);
            project.Country = InputSanitizer.Sanitize(project.Country);

            bool siteManagerChanged = existingProject.SiteManagerId != project.SiteManagerId;
            NotificationsController.LogPush($"[PUT] Project {id} Update. SiteManagerChanged: {siteManagerChanged} (Old: {existingProject.SiteManagerId} -> New: {project.SiteManagerId})");

            _context.Entry(existingProject).CurrentValues.SetValues(project);

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Update", id);

                if (siteManagerChanged && project.SiteManagerId.HasValue)
                {
                    await NotifySiteManagerAssignmentAsync(project, project.SiteManagerId);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProjectExists(id)) return NotFound();
                else throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project {Id}", id);
                return StatusCode(500, "An error occurred while updating the project.");
            }
            return NoContent();
        }

        /// <summary>
        /// Deletes a project by ID (soft-delete by default, or permanent delete if specified).
        /// </summary>
        /// <param name="id">The project ID to delete.</param>
        /// <param name="permanent">If true, hard deletes the project and related sub-entities.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProject(Guid id, [FromQuery] bool permanent = false)
        {
            if (id == Guid.Empty) return BadRequest("Invalid project ID.");

            try
            {
                if (permanent)
                {
                    var project = await _context.Projects
                        .Include(p => p.Tasks)
                            .ThenInclude(t => t.Comments)
                        .Include(p => p.TeamMembers)
                        .Include(p => p.VariationOrders)
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(p => p.Id == id);

                    if (project == null) return NotFound();

                    var hseqDocs = await _context.Set<HseqDocument>().Where(d => d.ProjectId == id).ToListAsync();
                    var hseqAudits = await _context.Set<HseqAudit>().Where(a => a.ProjectId == id).ToListAsync();
                    var incidents = await _context.Set<Incident>().Where(i => i.ProjectId == id).ToListAsync();
                    var snagJobs = await _context.Set<SnagJob>().Where(s => s.ProjectId == id).ToListAsync();

                    var auditIds = hseqAudits.Select(a => a.Id).ToList();
                    var hseqAttachments = auditIds.Any()
                        ? await _context.Set<HseqAuditAttachment>().Where(at => auditIds.Contains(at.AuditId)).ToListAsync()
                        : new List<HseqAuditAttachment>();

                    var attendanceRecords = await _context.AttendanceRecords.Where(a => a.ProjectId == id).ToListAsync();
                    foreach (var att in attendanceRecords)
                    {
                        att.ProjectId = null;
                    }

                    var taskIds = project.Tasks.Select(t => t.Id).ToList();
                    var timeRecords = await _context.TimeRecords
                        .Where(tr => tr.ProjectId == id || (tr.TaskId.HasValue && taskIds.Contains(tr.TaskId.Value)))
                        .ToListAsync();
                    foreach (var tr in timeRecords)
                    {
                        tr.ProjectId = null;
                        tr.TaskId = null;
                    }

                    _context.SupressSoftDelete = true;
                    
                    if (hseqDocs.Any()) _context.RemoveRange(hseqDocs);
                    if (hseqAttachments.Any()) _context.RemoveRange(hseqAttachments);
                    if (hseqAudits.Any()) _context.RemoveRange(hseqAudits);
                    if (incidents.Any()) _context.RemoveRange(incidents);
                    if (snagJobs.Any()) _context.RemoveRange(snagJobs);

                    _context.Projects.Remove(project);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogWarning("Project {Name} ({Id}) PERMANENTLY deleted by Admin including all related tasks and members", project.Name, id);
                }
                else
                {
                    var project = await _context.Projects
                        .Include(p => p.Tasks)
                        .Include(p => p.TeamMembers)
                        .Include(p => p.VariationOrders)
                        .FirstOrDefaultAsync(p => p.Id == id);

                    if (project == null) return NotFound();

                    project.IsActive = false;

                    foreach (var task in project.Tasks)
                    {
                        task.IsActive = false;
                    }

                    foreach (var tm in project.TeamMembers)
                    {
                        tm.IsActive = false;
                    }

                    foreach (var vo in project.VariationOrders)
                    {
                        vo.IsActive = false;
                    }

                    var taskIds = project.Tasks.Select(t => t.Id).ToList();
                    if (taskIds.Any())
                    {
                        var comments = await _context.TaskComments.Where(c => taskIds.Contains(c.TaskId)).ToListAsync();
                        foreach (var c in comments) c.IsActive = false;

                        var assignments = await _context.TaskAssignments.Where(a => taskIds.Contains(a.TaskId)).ToListAsync();
                        foreach (var a in assignments) a.IsActive = false;

                        var attachments = await _context.TaskAttachments.Where(at => taskIds.Contains(at.TaskId)).ToListAsync();
                        foreach (var at in attachments) at.IsActive = false;
                    }

                    var hseqDocs = await _context.Set<HseqDocument>().Where(d => d.ProjectId == id).ToListAsync();
                    foreach (var doc in hseqDocs) doc.IsActive = false;

                    var hseqAudits = await _context.Set<HseqAudit>().Where(a => a.ProjectId == id).ToListAsync();
                    foreach (var audit in hseqAudits) audit.IsActive = false;

                    var incidents = await _context.Set<Incident>().Where(i => i.ProjectId == id).ToListAsync();
                    foreach (var incident in incidents) incident.IsActive = false;

                    var snagJobs = await _context.Set<SnagJob>().Where(s => s.ProjectId == id).ToListAsync();
                    foreach (var snag in snagJobs) snag.IsActive = false;

                    var siteDeployments = await _context.Set<SiteDeployment>().Where(sd => sd.ProjectId == id).ToListAsync();
                    foreach (var sd in siteDeployments) sd.IsActive = false;

                    var drafts = await _context.ProjectReportDrafts.Where(d => d.ProjectId == id).ToListAsync();
                    foreach (var d in drafts) d.IsActive = false;

                    var historyRecords = await _context.ProjectReportHistories.Where(h => h.ProjectId == id).ToListAsync();
                    foreach (var h in historyRecords) h.IsActive = false;

                    await _context.SaveChangesAsync();
                }

                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Delete", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {Id}", id);
                return StatusCode(500, "An error occurred while deleting the project.");
            }
        }

        /// <summary>
        /// Restores a soft-deleted project entity.
        /// </summary>
        /// <param name="id">The ID of the project to restore.</param>
        /// <returns>The restored project entity.</returns>
        [HttpPost("{id}/restore")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RestoreProject(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid project ID.");

            try
            {
                var project = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
                if (project == null) return NotFound();

                if (project.IsActive) return BadRequest("Project is already active.");

                project.IsActive = true;
                project.UpdatedAtUtc = DateTime.UtcNow;
                project.UpdatedBy = User.Identity?.Name ?? "System";

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Update", id);

                return Ok(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring project {Id}", id);
                return StatusCode(500, "An error occurred while restoring the project.");
            }
        }

        /// <summary>
        /// Retrieves personnel allocations for a specific project.
        /// </summary>
        /// <param name="id">The project ID.</param>
        /// <returns>Project personnel DTO.</returns>
        [HttpGet("{id}/personnel")]
        public async Task<ActionResult<ProjectPersonnelDto>> GetProjectPersonnel(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid project ID.");

            try
            {
                var project = await _context.Projects
                    .Include(p => p.SiteManager)
                    .Include(p => p.TeamMembers)
                    .ThenInclude(tm => tm.Employee)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (project == null) return NotFound();

                Guid.TryParse(project.CreatedBy, out var creatorGuid);
                var creator = await _context.Users.FirstOrDefaultAsync(u => (creatorGuid != Guid.Empty && u.Id == creatorGuid) || u.Email == project.CreatedBy);
                var projectManager = creator?.DisplayName ?? creator?.Email ?? project.CreatedBy;

                var dto = new ProjectPersonnelDto
                {
                    ProjectId = project.Id,
                    SiteManagerId = project.SiteManagerId,
                    SiteManagerName = project.SiteManager?.DisplayName,
                    ProjectManager = projectManager,
                    TeamMembers = project.TeamMembers
                        .Where(tm => tm.Employee != null)
                        .Select(tm => new EmployeeSummaryDto
                        {
                            Id = tm.Employee!.Id,
                            FirstName = tm.Employee.FirstName,
                            LastName = tm.Employee.LastName,
                            Role = tm.Employee.Role,
                            Status = tm.Employee.Status
                        }).ToList()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving personnel for project {Id}", id);
                return StatusCode(500, "An error occurred while retrieving project personnel.");
            }
        }

        /// <summary>
        /// Updates project site manager and team member allocations.
        /// </summary>
        /// <param name="id">The project ID.</param>
        /// <param name="update">The update payload.</param>
        /// <returns>No content on success.</returns>
        [HttpPost("{id}/personnel")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> UpdateProjectPersonnel(Guid id, ProjectPersonnelUpdateDto update)
        {
            if (id == Guid.Empty) return BadRequest("Invalid project ID.");
            if (update == null) return BadRequest("Update payload cannot be null.");

            try
            {
                var project = await _context.Projects
                    .Include(p => p.TeamMembers)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (project == null) return NotFound();

                project.SiteManagerId = update.SiteManagerId;

                if (update.TeamMemberIds != null)
                {
                    var toRemove = project.TeamMembers.Where(tm => !update.TeamMemberIds.Contains(tm.EmployeeId)).ToList();
                    foreach (var tm in toRemove)
                    {
                        _context.ProjectTeamMembers.Remove(tm);
                    }

                    var existingIds = project.TeamMembers.Select(tm => tm.EmployeeId).ToList();
                    foreach (var empId in update.TeamMemberIds)
                    {
                        if (!existingIds.Contains(empId))
                        {
                            _context.ProjectTeamMembers.Add(new ProjectTeamMember
                            {
                                Id = Guid.NewGuid(),
                                ProjectId = id,
                                EmployeeId = empId,
                                DateAdded = DateTime.UtcNow
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Update", id);

                if (update.SiteManagerId.HasValue)
                {
                    await NotifySiteManagerAssignmentAsync(project, update.SiteManagerId);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating personnel for project {Id}", id);
                return StatusCode(500, "An error occurred while updating project personnel.");
            }
        }

        /// <summary>
        /// Retrieves historical activity and personnel entry logs for a project.
        /// </summary>
        /// <param name="id">The project ID.</param>
        /// <returns>Project history DTO.</returns>
        [HttpGet("{id}/history")]
        public async Task<ActionResult<ProjectHistoryDto>> GetProjectHistory(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid project ID.");

            try
            {
                var history = new ProjectHistoryDto { ProjectId = id };

                var attendance = await _context.AttendanceRecords
                    .Where(a => a.ProjectId == id)
                    .Join(_context.Employees, a => a.EmployeeId, e => e.Id, (a, e) => new { a, e })
                    .ToListAsync();

                var assignments = await _context.TaskAssignments
                    .Include(a => a.ProjectTask)
                    .Where(a => a.ProjectTask != null && a.ProjectTask.ProjectId == id)
                    .ToListAsync();

                var employeeEntries = attendance.GroupBy(x => x.e.Id)
                    .Select(g => new PersonnelHistoryEntryDto
                    {
                        Id = g.Key,
                        Name = g.First().e.DisplayName,
                        Role = g.First().e.Role.ToString(),
                        Type = "Staff",
                        DaysWorked = g.Select(x => x.a.Date.Date).Distinct().Count(),
                        TasksAssigned = assignments.Count(a => a.AssigneeId == g.Key),
                        FirstActive = g.Min(x => x.a.Date),
                        LastActive = g.Max(x => x.a.Date)
                    }).ToList();

                var contractorEntries = assignments
                    .Where(a => a.AssigneeType == AssigneeType.Contractor)
                    .GroupBy(a => a.AssigneeId)
                    .Select(g => new PersonnelHistoryEntryDto
                    {
                        Id = g.Key,
                        Name = g.First().AssigneeName,
                        Role = "Contractor",
                        Type = "Contractor",
                        DaysWorked = 0,
                        TasksAssigned = g.Count(),
                        FirstActive = null,
                        LastActive = null
                    }).ToList();

                history.Entries.AddRange(employeeEntries);
                history.Entries.AddRange(contractorEntries.Where(ce => !employeeEntries.Any(ee => ee.Id == ce.Id)));

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving history for project {Id}", id);
                return StatusCode(500, "An error occurred while retrieving project history.");
            }
        }

        /// <summary>
        /// Generates a comprehensive cost and labor breakdown report for a project.
        /// </summary>
        /// <param name="id">The project ID.</param>
        /// <returns>Project report DTO.</returns>
        [HttpGet("{id}/report")]
        public async Task<ActionResult<ProjectReportDto>> GetProjectReport(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid project ID.");

            try
            {
                var project = await _context.Projects
                    .Include(p => p.CustomerEntity)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (project == null) return NotFound();

                var report = new ProjectReportDto
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    ClientName = project.CustomerEntity?.Name ?? project.Customer ?? "Internal",
                    Status = project.Status,
                    StartDate = project.StartDate,
                    EndDate = project.EndDate
                };

                var orders = await _context.Orders
                    .Where(o => o.ProjectId == id)
                    .Include(o => o.Lines)
                    .AsNoTracking()
                    .ToListAsync();

                report.TotalMaterialCost = Math.Round((decimal)orders.Sum(o => o.Lines.Sum(l => l.LineTotal)), 2);
                report.LinkedOrders = orders.Select(o => ToSummaryDto(o)).OrderByDescending(o => o.OrderDate).ToList();

                var timeRecords = await _context.TimeRecords
                    .Where(tr => tr.ProjectId == id)
                    .Join(_context.Employees, tr => tr.EmployeeId, e => e.Id, (tr, e) => new { tr, e })
                    .AsNoTracking()
                    .ToListAsync();

                report.LabourBreakdown = timeRecords
                    .GroupBy(x => x.e.DisplayName)
                    .Select(g => new LabourDetailDto
                    {
                        EmployeeName = g.Key,
                        Hours = Math.Round(g.Sum(x => x.tr.Hours), 2),
                        HourlyRate = Math.Round((decimal)g.First().e.HourlyRate, 2)
                    }).ToList();

                report.TotalLabourCost = Math.Round(report.LabourBreakdown.Sum(l => l.TotalCost), 2);

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating project report for {Id}", id);
                return StatusCode(500, "An error occurred while generating project report.");
            }
        }

        /// <summary>
        /// Synchronizes and links orphaned projects to the currently authenticated site manager employee.
        /// </summary>
        /// <returns>Result of assignment sync operation.</returns>
        [HttpPost("sync-assignments")]
        [Authorize]
        public async Task<IActionResult> SyncAssignments()
        {
            var userEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == userEmail);
            if (employee == null) return NotFound("Employee record not found.");

            var ghostId = Guid.Parse("17c72266-ce66-4144-b7a8-d17dd58b78f5");
            var myFullName = $"{employee.FirstName} {employee.LastName}";
            var myInvertedName = $"{employee.LastName}, {employee.FirstName}";

            var orphanedProjects = await _context.Projects
                .Include(p => p.SiteManager)
                .Where(p => p.SiteManagerId == ghostId || 
                           (p.SiteManagerId != employee.Id && p.SiteManager != null && 
                            (p.SiteManager.Email == employee.Email || 
                             p.SiteManager.FirstName + " " + p.SiteManager.LastName == myFullName ||
                             p.SiteManager.LastName + ", " + p.SiteManager.FirstName == myInvertedName)))
                .ToListAsync();

            try
            {
                if (orphanedProjects.Any())
                {
                    foreach (var p in orphanedProjects)
                    {
                        p.SiteManagerId = employee.Id;
                    }
                    await _context.SaveChangesAsync();
                    return Ok(new { Success = true, Count = orphanedProjects.Count, Message = $"Successfully re-assigned {orphanedProjects.Count} projects to {employee.DisplayName}." });
                }
                return Ok(new { Success = true, Count = 0, Message = "No orphaned projects found for this identity." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing project assignments for {UserEmail}", userEmail);
                return StatusCode(500, "An error occurred while syncing project assignments.");
            }
        }

        /// <summary>
        /// Imports a batch of project tasks into a project, replacing existing tasks in a transactional scope.
        /// </summary>
        /// <param name="id">The project ID.</param>
        /// <param name="tasks">The list of tasks to import.</param>
        /// <returns>Result of task import operation.</returns>
        [HttpPost("{id}/import-tasks")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> ImportTasks(Guid id, [FromBody] List<ProjectTask> tasks)
        {
            if (id == Guid.Empty) return BadRequest("Invalid project ID.");
            if (tasks == null) return BadRequest("Tasks collection cannot be null.");

            var projectExists = await _context.Projects.AnyAsync(p => p.Id == id);
            if (!projectExists) return NotFound($"Project with ID {id} not found.");

            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var existingTaskIds = await _context.ProjectTasks
                            .Where(t => t.ProjectId == id)
                            .Select(t => t.Id)
                            .ToListAsync();

                        if (existingTaskIds.Any())
                        {
                            var assignments = _context.TaskAssignments.Where(a => existingTaskIds.Contains(a.TaskId));
                            _context.TaskAssignments.RemoveRange(assignments);

                            var comments = _context.TaskComments.Where(c => existingTaskIds.Contains(c.TaskId));
                            _context.TaskComments.RemoveRange(comments);

                            var attachments = _context.TaskAttachments.Where(a => existingTaskIds.Contains(a.TaskId));
                            _context.TaskAttachments.RemoveRange(attachments);

                            var existingTasks = _context.ProjectTasks.Where(t => t.ProjectId == id);
                            _context.ProjectTasks.RemoveRange(existingTasks);
                        }

                        foreach (var task in tasks)
                        {
                            if (task.Id == Guid.Empty) task.Id = Guid.NewGuid();
                            task.Name = InputSanitizer.Sanitize(task.Name);
                            task.ProjectId = id;
                            task.Project = null;
                            task.Children = new List<ProjectTask>();
                            
                            _context.ProjectTasks.Add(task);
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Update", id);
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "ProjectTask", "BatchUpdate", id);

                return Ok(new { Success = true, Count = tasks.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing tasks for project {Id}", id);
                return StatusCode(500, "An error occurred while importing tasks.");
            }
        }

        private static OrderSummaryDto ToSummaryDto(Order o)
        {
            return new OrderSummaryDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                OrderDate = o.OrderDate,
                OrderType = o.OrderType,
                Status = o.Status,
                TotalAmount = Math.Round(o.TotalAmount, 2),
                Branch = o.Branch.ToString(),
                ProjectName = o.ProjectName ?? string.Empty,
                SupplierName = o.SupplierName
            };
        }

        private bool ProjectExists(Guid id) => _context.Projects.Any(e => e.Id == id);

        private async Task NotifySiteManagerAssignmentAsync(Project project, Guid? siteManagerId)
        {
            NotificationsController.LogPush($"[NOTIFY] Start for Project {project.Name} to SM {siteManagerId}");
            if (!siteManagerId.HasValue) return;

            try
            {
                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == siteManagerId.Value);
                if (employee != null)
                {
                    Guid? targetUserId = employee.LinkedUserId;
                    NotificationsController.LogPush($"[NOTIFY] Employee {employee.DisplayName} found. LinkedUserId: {targetUserId}");
                    
                    if (!targetUserId.HasValue && !string.IsNullOrEmpty(employee.Email))
                    {
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == employee.Email);
                        if (user != null)
                        {
                            targetUserId = user.Id;
                            employee.LinkedUserId = user.Id;
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Auto-linked Employee {Emp} to User {User} via Email during notification", employee.DisplayName, user.Id);
                            NotificationsController.LogPush($"[NOTIFY] Auto-linked via email: {user.Email} -> {user.Id}");
                        }
                    }

                    if (targetUserId.HasValue)
                    {
                        NotificationsController.LogPush($"[NOTIFY] Triggering Push to User {targetUserId.Value}");
                        await _notificationService.SendPushNotificationAsync(
                            targetUserId.Value,
                            "New Project Assigned",
                            $"You have been assigned as the Site Manager for: {project.Name}");
                        
                        _logger.LogInformation("Sent assignment push notification to Site Manager {Name} (User {UserId}) for project {Project}", 
                            employee.DisplayName, targetUserId.Value, project.Name);
                    }
                    else
                    {
                        NotificationsController.LogPush($"[NOTIFY] ABORTED: No linked User account for {employee.DisplayName}");
                        _logger.LogWarning("Skipped push notification for Site Manager {Name}: No linked User account found.", employee.DisplayName);
                    }
                }
                else
                {
                    NotificationsController.LogPush($"[NOTIFY] ABORTED: Employee {siteManagerId} not found in DB.");
                }
            }
            catch (Exception ex)
            {
                NotificationsController.LogPush($"[NOTIFY] ERROR: {ex.Message}");
                _logger.LogError(ex, "Failed to send push notification for project assignment to Site Manager {Id}", siteManagerId);
            }
        }
    }
}
