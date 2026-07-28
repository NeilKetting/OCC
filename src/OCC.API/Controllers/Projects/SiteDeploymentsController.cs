using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.API.Security;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for managing site deployments, crew allocations, daily site manager receipts with GPS verification, and active site personnel tracking.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SiteDeploymentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SiteDeploymentsController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="SiteDeploymentsController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="hubContext">SignalR notification hub context.</param>
        public SiteDeploymentsController(
            AppDbContext context,
            ILogger<SiteDeploymentsController> logger,
            IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Retrieves site deployments matching the specified query filters.
        /// </summary>
        /// <param name="projectId">Optional project ID filter.</param>
        /// <param name="date">Optional deployment date filter.</param>
        /// <param name="siteManagerId">Optional site manager ID filter.</param>
        /// <param name="status">Optional deployment status filter.</param>
        /// <returns>A list of site deployment DTOs.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SiteDeploymentDto>>> GetDeployments(
            [FromQuery] Guid? projectId = null,
            [FromQuery] DateTime? date = null,
            [FromQuery] Guid? siteManagerId = null,
            [FromQuery] DeploymentStatus? status = null)
        {
            try
            {
                var query = _context.SiteDeployments
                    .AsNoTracking()
                    .Include(sd => sd.Project)
                    .Include(sd => sd.ReceivedBySiteManager)
                    .Include(sd => sd.Members)
                        .ThenInclude(m => m.Employee)
                    .AsQueryable();

                if (projectId.HasValue)
                {
                    if (projectId.Value == Guid.Empty) return BadRequest("Invalid project ID.");
                    query = query.Where(sd => sd.ProjectId == projectId.Value);
                }

                if (date.HasValue)
                    query = query.Where(sd => sd.DeploymentDate.Date == date.Value.Date);

                if (status.HasValue)
                    query = query.Where(sd => sd.Status == status.Value);

                if (siteManagerId.HasValue)
                {
                    if (siteManagerId.Value == Guid.Empty) return BadRequest("Invalid site manager ID.");
                    query = query.Where(sd => sd.Project != null && sd.Project.SiteManagerId == siteManagerId.Value);
                }

                var deployments = await query
                    .OrderByDescending(sd => sd.DeploymentDate)
                    .ThenBy(sd => sd.Label)
                    .ToListAsync();

                return Ok(deployments.Select(sd => ToDto(sd)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving site deployments");
                return StatusCode(500, "An error occurred while retrieving site deployments.");
            }
        }

        /// <summary>
        /// Retrieves details for a specific site deployment by its ID.
        /// </summary>
        /// <param name="id">The site deployment ID.</param>
        /// <returns>The requested site deployment DTO.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<SiteDeploymentDto>> GetDeployment(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid deployment ID.");

            try
            {
                var sd = await _context.SiteDeployments
                    .AsNoTracking()
                    .Include(sd => sd.Project)
                    .Include(sd => sd.ReceivedBySiteManager)
                    .Include(sd => sd.Members).ThenInclude(m => m.Employee)
                    .FirstOrDefaultAsync(sd => sd.Id == id);

                if (sd == null) return NotFound();
                return Ok(ToDto(sd));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving site deployment {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the site deployment.");
            }
        }

        /// <summary>
        /// Creates a new site deployment record with crew members.
        /// </summary>
        /// <param name="request">The creation request payload.</param>
        /// <returns>The created site deployment DTO.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<ActionResult<SiteDeploymentDto>> CreateDeployment([FromBody] CreateSiteDeploymentRequest request)
        {
            if (request == null) return BadRequest("Deployment request cannot be null.");
            if (request.ProjectId == Guid.Empty) return BadRequest("Invalid project ID.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var project = await _context.Projects.FindAsync(request.ProjectId);
                if (project == null) return NotFound("Project not found.");

                var deployment = new SiteDeployment
                {
                    Id = Guid.NewGuid(),
                    ProjectId = request.ProjectId,
                    DeploymentDate = request.DeploymentDate.Date,
                    Label = InputSanitizer.Sanitize(request.Label),
                    Status = DeploymentStatus.Pending
                };

                if (request.MemberEmployeeIds != null)
                {
                    foreach (var empId in request.MemberEmployeeIds.Distinct())
                    {
                        if (empId == Guid.Empty) continue;
                        var employee = await _context.Employees.FindAsync(empId);
                        if (employee == null) continue;

                        deployment.Members.Add(new SiteDeploymentMember
                        {
                            Id = Guid.NewGuid(),
                            SiteDeploymentId = deployment.Id,
                            EmployeeId = empId
                        });
                    }
                }

                _context.SiteDeployments.Add(deployment);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SiteDeployment", "Create", deployment.Id.ToString());

                var created = await _context.SiteDeployments
                    .AsNoTracking()
                    .Include(sd => sd.Project)
                    .Include(sd => sd.Members).ThenInclude(m => m.Employee)
                    .FirstOrDefaultAsync(sd => sd.Id == deployment.Id);

                return CreatedAtAction(nameof(GetDeployment), new { id = deployment.Id }, ToDto(created!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating site deployment");
                return StatusCode(500, "An error occurred while creating the site deployment.");
            }
        }

        /// <summary>
        /// Updates an existing pending site deployment.
        /// </summary>
        /// <param name="id">The deployment ID.</param>
        /// <param name="request">The update request payload.</param>
        /// <returns>No content on success.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> UpdateDeployment(Guid id, [FromBody] CreateSiteDeploymentRequest request)
        {
            if (id == Guid.Empty) return BadRequest("Invalid deployment ID.");
            if (request == null) return BadRequest("Deployment request cannot be null.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var deployment = await _context.SiteDeployments
                    .Include(sd => sd.Members)
                    .FirstOrDefaultAsync(sd => sd.Id == id);

                if (deployment == null) return NotFound();
                if (deployment.Status != DeploymentStatus.Pending)
                    return Conflict("Only Pending deployments can be edited.");

                deployment.Label = InputSanitizer.Sanitize(request.Label);
                deployment.DeploymentDate = request.DeploymentDate.Date;

                _context.SiteDeploymentMembers.RemoveRange(deployment.Members);
                if (request.MemberEmployeeIds != null)
                {
                    foreach (var empId in request.MemberEmployeeIds.Distinct())
                    {
                        if (empId == Guid.Empty) continue;
                        deployment.Members.Add(new SiteDeploymentMember
                        {
                            Id = Guid.NewGuid(),
                            SiteDeploymentId = id,
                            EmployeeId = empId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SiteDeployment", "Update", id.ToString());

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating site deployment {Id}", id);
                return StatusCode(500, "An error occurred while updating the site deployment.");
            }
        }

        /// <summary>
        /// Cancels a site deployment.
        /// </summary>
        /// <param name="id">The deployment ID.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> CancelDeployment(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid deployment ID.");

            try
            {
                var deployment = await _context.SiteDeployments.FindAsync(id);
                if (deployment == null) return NotFound();

                if (deployment.Status == DeploymentStatus.Received)
                    return Conflict("Cannot cancel a deployment that has already been received.");

                deployment.Status = DeploymentStatus.Cancelled;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SiteDeployment", "Cancel", id.ToString());

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling site deployment {Id}", id);
                return StatusCode(500, "An error occurred while cancelling the site deployment.");
            }
        }

        /// <summary>
        /// Confirms crew arrival on-site, records GPS verification coordinates, marks absent crew members, and links attendance to the project.
        /// </summary>
        /// <param name="id">The site deployment ID.</param>
        /// <param name="request">The receipt request containing site manager ID, GPS coords, and absent member IDs.</param>
        /// <returns>Distance from site in metres.</returns>
        [HttpPost("{id}/receive")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> ReceiveDeployment(Guid id, [FromBody] ReceiveDeploymentRequest request)
        {
            if (id == Guid.Empty) return BadRequest("Invalid deployment ID.");
            if (request == null) return BadRequest("Receive request cannot be null.");
            if (request.SiteManagerId == Guid.Empty) return BadRequest("Invalid Site Manager ID.");

            // Validate GPS ranges if provided
            if (request.GpsLatitude.HasValue && (request.GpsLatitude.Value < -90.0 || request.GpsLatitude.Value > 90.0))
            {
                return BadRequest("Invalid GPS latitude coordinate.");
            }
            if (request.GpsLongitude.HasValue && (request.GpsLongitude.Value < -180.0 || request.GpsLongitude.Value > 180.0))
            {
                return BadRequest("Invalid GPS longitude coordinate.");
            }

            try
            {
                var deployment = await _context.SiteDeployments
                    .Include(sd => sd.Project)
                    .Include(sd => sd.Members).ThenInclude(m => m.Employee)
                    .FirstOrDefaultAsync(sd => sd.Id == id);

                if (deployment == null) return NotFound();
                if (deployment.Status != DeploymentStatus.Pending)
                    return Conflict($"Deployment is already {deployment.Status}.");

                var today = DateTime.UtcNow.Date;

                double? distance = null;
                if (request.GpsLatitude.HasValue && request.GpsLongitude.HasValue
                    && deployment.Project?.Latitude.HasValue == true
                    && deployment.Project?.Longitude.HasValue == true)
                {
                    distance = CalculateDistanceMetres(
                        request.GpsLatitude.Value, request.GpsLongitude.Value,
                        deployment.Project.Latitude!.Value, deployment.Project.Longitude!.Value);
                }

                deployment.Status = DeploymentStatus.Received;
                deployment.ReceivedAt = DateTime.UtcNow;
                deployment.ReceivedBySiteManagerId = request.SiteManagerId;
                deployment.ReceivedGpsLatitude = request.GpsLatitude;
                deployment.ReceivedGpsLongitude = request.GpsLongitude;
                deployment.DistanceFromSiteMetres = distance;

                var absentSet = request.AbsentMemberEmployeeIds != null 
                    ? new HashSet<Guid>(request.AbsentMemberEmployeeIds) 
                    : new HashSet<Guid>();

                foreach (var member in deployment.Members)
                {
                    member.IsAbsent = absentSet.Contains(member.EmployeeId);

                    if (!member.IsAbsent)
                    {
                        var attendance = await _context.AttendanceRecords
                            .FirstOrDefaultAsync(r =>
                                r.EmployeeId == member.EmployeeId &&
                                r.Date.Date == today);

                        if (attendance != null && attendance.ProjectId == null)
                        {
                            attendance.ProjectId = deployment.ProjectId;
                        }
                    }
                }

                var smAttendance = await _context.AttendanceRecords
                    .FirstOrDefaultAsync(r =>
                        r.EmployeeId == request.SiteManagerId &&
                        r.Date.Date == today);

                if (smAttendance != null && smAttendance.ProjectId == null)
                {
                    smAttendance.ProjectId = deployment.ProjectId;
                }

                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SiteDeployment", "Received", id.ToString());

                _logger.LogInformation(
                    "Deployment {Id} received by SM {SiteManagerId}. Present: {PresentCount}, Absent: {AbsentCount}. Distance from site: {Distance}m",
                    id, request.SiteManagerId,
                    deployment.Members.Count(m => !m.IsAbsent),
                    deployment.Members.Count(m => m.IsAbsent),
                    distance?.ToString("F0") ?? "N/A");

                return Ok(new { distance });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving site deployment {Id}", id);
                return StatusCode(500, "An error occurred while receiving the site deployment.");
            }
        }

        /// <summary>
        /// Retrieves active employees who have an attendance record for today (used for crew building).
        /// </summary>
        /// <returns>A list of employee summary DTOs clocked in today.</returns>
        [HttpGet("today-clocked-in")]
        public async Task<ActionResult<IEnumerable<EmployeeSummaryDto>>> GetTodayClockedIn()
        {
            try
            {
                var today = DateTime.UtcNow.Date;

                var employeeIds = await _context.AttendanceRecords
                    .AsNoTracking()
                    .Where(r => r.Date.Date == today && r.EmployeeId != null)
                    .Select(r => r.EmployeeId!.Value)
                    .Distinct()
                    .ToListAsync();

                var employees = await _context.Employees
                    .AsNoTracking()
                    .Where(e => employeeIds.Contains(e.Id) && e.Status == EmployeeStatus.Active)
                    .OrderBy(e => e.LastName)
                    .ToListAsync();

                var dtos = employees.Select(e => new EmployeeSummaryDto
                {
                    Id = e.Id,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Role = e.Role,
                    Status = e.Status,
                    Branch = e.Branch
                }).ToList();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving today's clocked-in employees");
                return StatusCode(500, "An error occurred while retrieving clocked-in employees.");
            }
        }

        private static SiteDeploymentDto ToDto(SiteDeployment sd) => new()
        {
            Id = sd.Id,
            ProjectId = sd.ProjectId,
            ProjectName = sd.Project?.Name ?? string.Empty,
            ProjectLatitude = sd.Project?.Latitude,
            ProjectLongitude = sd.Project?.Longitude,
            DeploymentDate = sd.DeploymentDate,
            Label = sd.Label,
            Status = sd.Status,
            ReceivedAt = sd.ReceivedAt,
            ReceivedBySiteManagerName = sd.ReceivedBySiteManager?.DisplayName,
            Members = sd.Members.Select(m => new SiteDeploymentMemberDto
            {
                Id = m.Id,
                EmployeeId = m.EmployeeId,
                FullName = m.Employee?.DisplayName ?? string.Empty,
                Role = m.Employee?.Role.ToString() ?? string.Empty,
                Initials = BuildInitials(m.Employee),
                IsAbsent = m.IsAbsent
            }).ToList()
        };

        private static string BuildInitials(Employee? e)
        {
            if (e == null) return "??";
            var f = e.FirstName.FirstOrDefault();
            var l = e.LastName.FirstOrDefault();
            return $"{f}{l}".ToUpper();
        }

        private static double CalculateDistanceMetres(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
