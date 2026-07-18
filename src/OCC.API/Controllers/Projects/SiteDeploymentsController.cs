using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SiteDeploymentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SiteDeploymentsController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public SiteDeploymentsController(
            AppDbContext context,
            ILogger<SiteDeploymentsController> logger,
            IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        // GET: api/sitedeployments?projectId=&date=&siteManagerId=
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
                    query = query.Where(sd => sd.ProjectId == projectId.Value);

                if (date.HasValue)
                    query = query.Where(sd => sd.DeploymentDate.Date == date.Value.Date);

                if (status.HasValue)
                    query = query.Where(sd => sd.Status == status.Value);

                // When filtering by siteManagerId, match deployments for projects where that SM is assigned
                if (siteManagerId.HasValue)
                    query = query.Where(sd => sd.Project != null && sd.Project.SiteManagerId == siteManagerId.Value);

                var deployments = await query
                    .OrderByDescending(sd => sd.DeploymentDate)
                    .ThenBy(sd => sd.Label)
                    .ToListAsync();

                return Ok(deployments.Select(sd => ToDto(sd)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving site deployments");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/sitedeployments/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<SiteDeploymentDto>> GetDeployment(Guid id)
        {
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
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/sitedeployments
        [HttpPost]
        public async Task<ActionResult<SiteDeploymentDto>> CreateDeployment([FromBody] CreateSiteDeploymentRequest request)
        {
            try
            {
                var project = await _context.Projects.FindAsync(request.ProjectId);
                if (project == null) return NotFound("Project not found");

                var deployment = new SiteDeployment
                {
                    Id = Guid.NewGuid(),
                    ProjectId = request.ProjectId,
                    DeploymentDate = request.DeploymentDate.Date,
                    Label = request.Label,
                    Status = DeploymentStatus.Pending
                };

                // Add members
                foreach (var empId in request.MemberEmployeeIds.Distinct())
                {
                    var employee = await _context.Employees.FindAsync(empId);
                    if (employee == null) continue;

                    deployment.Members.Add(new SiteDeploymentMember
                    {
                        Id = Guid.NewGuid(),
                        SiteDeploymentId = deployment.Id,
                        EmployeeId = empId
                    });
                }

                _context.SiteDeployments.Add(deployment);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SiteDeployment", "Create", deployment.Id.ToString());

                // Reload for full DTO
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
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/sitedeployments/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDeployment(Guid id, [FromBody] CreateSiteDeploymentRequest request)
        {
            try
            {
                var deployment = await _context.SiteDeployments
                    .Include(sd => sd.Members)
                    .FirstOrDefaultAsync(sd => sd.Id == id);

                if (deployment == null) return NotFound();
                if (deployment.Status != DeploymentStatus.Pending)
                    return Conflict("Only Pending deployments can be edited.");

                deployment.Label = request.Label;
                deployment.DeploymentDate = request.DeploymentDate.Date;

                // Replace members
                _context.SiteDeploymentMembers.RemoveRange(deployment.Members);
                foreach (var empId in request.MemberEmployeeIds.Distinct())
                {
                    deployment.Members.Add(new SiteDeploymentMember
                    {
                        Id = Guid.NewGuid(),
                        SiteDeploymentId = id,
                        EmployeeId = empId
                    });
                }

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SiteDeployment", "Update", id.ToString());

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating site deployment {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/sitedeployments/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelDeployment(Guid id)
        {
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
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/sitedeployments/{id}/receive
        /// <summary>
        /// Called by the Site Manager on the tablet to confirm the crew is on-site.
        /// This action:
        ///   1. Flips deployment Status → Received
        ///   2. Records GPS + timestamp
        ///   3. Marks specified members as absent
        ///   4. Sets AttendanceRecord.ProjectId for all present members (and the SM)
        /// </summary>
        [HttpPost("{id}/receive")]
        public async Task<IActionResult> ReceiveDeployment(Guid id, [FromBody] ReceiveDeploymentRequest request)
        {
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

                // Calculate distance from project site if GPS provided
                double? distance = null;
                if (request.GpsLatitude.HasValue && request.GpsLongitude.HasValue
                    && deployment.Project?.Latitude.HasValue == true
                    && deployment.Project?.Longitude.HasValue == true)
                {
                    distance = CalculateDistanceMetres(
                        request.GpsLatitude.Value, request.GpsLongitude.Value,
                        deployment.Project.Latitude!.Value, deployment.Project.Longitude!.Value);
                }

                // Update deployment record
                deployment.Status = DeploymentStatus.Received;
                deployment.ReceivedAt = DateTime.UtcNow;
                deployment.ReceivedBySiteManagerId = request.SiteManagerId;
                deployment.ReceivedGpsLatitude = request.GpsLatitude;
                deployment.ReceivedGpsLongitude = request.GpsLongitude;
                deployment.DistanceFromSiteMetres = distance;

                // Mark absent members + attribute attendance to project for present members
                var absentSet = new HashSet<Guid>(request.AbsentMemberEmployeeIds);
                foreach (var member in deployment.Members)
                {
                    member.IsAbsent = absentSet.Contains(member.EmployeeId);

                    if (!member.IsAbsent)
                    {
                        // Attribute today's attendance record to this project
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

                // Attribute the Site Manager's own attendance record to the project
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
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/sitedeployments/today-clocked-in
        /// <summary>
        /// Returns employees who have an attendance record for today — used by the
        /// WPF crew builder to show who is available to be allocated to a crew.
        /// </summary>
        [HttpGet("today-clocked-in")]
        public async Task<ActionResult<IEnumerable<EmployeeSummaryDto>>> GetTodayClockedIn()
        {
            try
            {
                var today = DateTime.UtcNow.Date;

                // Get EmployeeIds that have an attendance record today
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
                return StatusCode(500, "Internal server error");
            }
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

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

        /// <summary> Haversine formula — returns distance in metres between two GPS coordinates. </summary>
        private static double CalculateDistanceMetres(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Earth radius in metres
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
