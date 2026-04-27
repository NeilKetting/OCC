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
    public class ProjectsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<ProjectsController> _logger;
        private readonly Services.INotificationService _notificationService;

        public ProjectsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<ProjectsController> logger, Services.INotificationService notificationService)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
            _notificationService = notificationService;
        }

        [HttpGet("summaries")]
        public async Task<ActionResult<IEnumerable<ProjectSummaryDto>>> GetProjectSummaries()
        {
            try
            {
                var query = _context.Projects
                    .Include(p => p.Tasks)
                    .Include(p => p.SiteManager)
                    .AsNoTracking();

                var projects = await query.ToListAsync();

                var summaries = projects.Select(p => new ProjectSummaryDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Status = p.Status,
                    ProjectManager = p.ProjectManager,
                    TaskCount = p.Tasks.Count,
                    Progress = p.Tasks.Any() ? (int)Math.Round(p.Tasks.Average(t => (double)t.PercentComplete)) : 0,
                    LatestFinish = p.Tasks.Any() ? p.Tasks.Max(t => t.FinishDate) : p.EndDate,
                    StartDate = p.StartDate,
                    SiteManagerId = p.SiteManagerId,
                    SiteManagerName = p.SiteManager?.DisplayName ?? "Unassigned"
                }).ToList();

                return Ok(summaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project summaries");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Projects
        // GET: api/Projects
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
                        return await query.ToListAsync();
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

                    if (linkedEmployee == null)
                    {
                        return new List<Project>();
                    }

                    query = query.Where(p => 
                        p.SiteManagerId == linkedEmployee.Id || 
                        p.Tasks.Any(t => t.Assignments.Any(a => a.AssigneeId == linkedEmployee.Id))
                    );
                }

                var projectIds = await query.Select(p => p.Id).Distinct().ToListAsync();
                
                return await _context.Projects
                    .Include(p => p.Tasks)
                    .Where(p => projectIds.Contains(p.Id))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Project>> GetProject(Guid id)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.Tasks)
                    .ThenInclude(t => t.Assignments)
                    .Include(p => p.Tasks)
                    .ThenInclude(t => t.Comments)
                    .Include(p => p.SiteManager) // Added
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (project == null) return NotFound();
                return project;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Projects
        [HttpPost]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<ActionResult<Project>> PostProject(Project project)
        {
            try
            {
                if (project.Id == Guid.Empty) project.Id = Guid.NewGuid();
                
                // Set Project Manager to current user if not provided
                if (string.IsNullOrEmpty(project.ProjectManager))
                {
                    var userEmail = User.Identity?.Name;
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                        project.ProjectManager = user?.DisplayName ?? userEmail;
                    }
                }

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Create", project.Id);

                return CreatedAtAction("GetProject", new { id = project.Id }, project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Projects/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> PutProject(Guid id, Project project)
        {
            if (id != project.Id) return BadRequest();
            _context.Entry(project).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProjectExists(id)) return NotFound();
                else throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project {Id}", id);
                return StatusCode(500, "Internal server error");
            }
            return NoContent();
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProject(Guid id)
        {
            try
            {
                var project = await _context.Projects.FindAsync(id);
                if (project == null) return NotFound();
                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("EntityUpdate", "Project", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/personnel")]
        public async Task<ActionResult<ProjectPersonnelDto>> GetProjectPersonnel(Guid id)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.SiteManager)
                    .Include(p => p.TeamMembers)
                    .ThenInclude(tm => tm.Employee)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (project == null) return NotFound();

                var dto = new ProjectPersonnelDto
                {
                    ProjectId = project.Id,
                    SiteManagerId = project.SiteManagerId,
                    SiteManagerName = project.SiteManager?.DisplayName,
                    ProjectManager = project.ProjectManager,
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
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/personnel")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> UpdateProjectPersonnel(Guid id, ProjectPersonnelUpdateDto update)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.TeamMembers)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (project == null) return NotFound();

                // Update Site Manager
                project.SiteManagerId = update.SiteManagerId;

                // Update Team Members (if provided)
                if (update.TeamMemberIds != null)
                {
                    // 1. Remove members not in the new list
                    var toRemove = project.TeamMembers.Where(tm => !update.TeamMemberIds.Contains(tm.EmployeeId)).ToList();
                    foreach (var tm in toRemove) project.TeamMembers.Remove(tm);

                    // 2. Add new members
                    var existingIds = project.TeamMembers.Select(tm => tm.EmployeeId).ToList();
                    foreach (var empId in update.TeamMemberIds)
                    {
                        if (!existingIds.Contains(empId))
                        {
                            project.TeamMembers.Add(new ProjectTeamMember
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

                // --- NEW: SEND PUSH NOTIFICATION ---
                if (update.SiteManagerId.HasValue)
                {
                    try
                    {
                        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == update.SiteManagerId.Value);
                        if (employee != null && employee.LinkedUserId.HasValue)
                        {
                            await _notificationService.SendPushNotificationAsync(
                                employee.LinkedUserId.Value,
                                "New Project Assigned",
                                $"You have been assigned as the Site Manager for: {project.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send push notification for project assignment");
                    }
                }
                // ------------------------------------

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating personnel for project {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/history")]
        public async Task<ActionResult<ProjectHistoryDto>> GetProjectHistory(Guid id)
        {
            try
            {
                var history = new ProjectHistoryDto { ProjectId = id };

                // 1. Get Employee History from AttendanceRecords
                var attendance = await _context.AttendanceRecords
                    .Where(a => a.ProjectId == id)
                    .Join(_context.Employees, a => a.EmployeeId, e => e.Id, (a, e) => new { a, e })
                    .ToListAsync();

                // 2. Get Assignments (includes potential contractors)
                var assignments = await _context.TaskAssignments
                    .Include(a => a.ProjectTask)
                    .Where(a => a.ProjectTask != null && a.ProjectTask.ProjectId == id)
                    .ToListAsync();

                // 3. Process Employees
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

                // 4. Process Contractors (who might not have attendance)
                var contractorIds = assignments.Where(a => a.AssigneeType == AssigneeType.Contractor).Select(a => a.AssigneeId).Distinct();
                var contractorEntries = assignments
                    .Where(a => a.AssigneeType == AssigneeType.Contractor)
                    .GroupBy(a => a.AssigneeId)
                    .Select(g => new PersonnelHistoryEntryDto
                    {
                        Id = g.Key,
                        Name = g.First().AssigneeName,
                        Role = "Contractor",
                        Type = "Contractor",
                        DaysWorked = 0, // We don't track attendance for contractors in this simplified logic
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
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/report")]
        public async Task<ActionResult<ProjectReportDto>> GetProjectReport(Guid id)
        {
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

                // Material Costs (Orders linked to this project)
                var orders = await _context.Orders
                    .Where(o => o.ProjectId == id)
                    .Include(o => o.Lines)
                    .AsNoTracking()
                    .ToListAsync();

                report.TotalMaterialCost = (decimal)orders.Sum(o => o.Lines.Sum(l => l.LineTotal));
                report.LinkedOrders = orders.Select(o => ToSummaryDto(o)).OrderByDescending(o => o.OrderDate).ToList();

                // Labour Costs (TimeRecords linked to project)
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
                        Hours = g.Sum(x => x.tr.Hours),
                        HourlyRate = (decimal)g.First().e.HourlyRate
                    }).ToList();

                report.TotalLabourCost = report.LabourBreakdown.Sum(l => l.TotalCost);

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating project report for {Id}", id);
                return StatusCode(500, "Internal server error");
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
                TotalAmount = o.TotalAmount,
                Branch = o.Branch.ToString(),
                ProjectName = o.ProjectName ?? string.Empty,
                SupplierName = o.SupplierName
            };
        }

        private bool ProjectExists(Guid id) => _context.Projects.Any(e => e.Id == id);

        [HttpPost("sync-assignments")]
        [Authorize]
        public async Task<IActionResult> SyncAssignments()
        {
            var userEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == userEmail);
            if (employee == null) return NotFound("Employee record not found.");

            // Known ghost ID from diagnostics
            var ghostId = Guid.Parse("17c72266-ce66-4144-b7a8-d17dd58b78f5");
            var myFullName = $"{employee.FirstName} {employee.LastName}";
            var myInvertedName = $"{employee.LastName}, {employee.FirstName}";

            // Find projects assigned to the ghost ID (Hard Override) OR a name match
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
                return Ok(new { Success = false, Count = 0, Message = $"DB Error: {ex.Message}" });
            }
        }
    }
}
