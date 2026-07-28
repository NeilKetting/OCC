using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OCC.API.Data;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.Shared.Framework;
using OCC.API.Hubs;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for managing employee records, employment statuses, and reference lookups.
    /// Supports role-based access control and real-time updates via SignalR.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Allow any authenticated user to READ (Get)
    public class EmployeesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EmployeesController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmployeesController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="hubContext">The SignalR notification hub context.</param>
        public EmployeesController(AppDbContext context, ILogger<EmployeesController> logger, IHubContext<NotificationHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        /// <summary>
        /// Retrieves all employees as summary DTOs ordered by last name.
        /// </summary>
        /// <returns>A list of <see cref="EmployeeSummaryDto"/> objects.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EmployeeSummaryDto>>> GetEmployees()
        {
            try
            {
                var employees = await _context.Employees
                    .AsNoTracking()
                    .OrderBy(e => e.LastName)
                    .Select(e => new EmployeeSummaryDto
                    {
                        Id = e.Id,
                        LinkedUserId = e.LinkedUserId,
                        FirstName = e.FirstName,
                        LastName = e.LastName,
                        IdNumber = e.IdNumber,
                        Email = e.Email,
                        Phone = e.Phone,
                        EmployeeNumber = e.EmployeeNumber,
                        Role = e.Role,
                        Status = e.Status,
                        EmploymentType = e.EmploymentType,
                        Branch = e.Branch,
                        RateType = e.RateType,
                        HourlyRate = e.HourlyRate,
                        ShiftStartTime = e.ShiftStartTime,
                        ShiftEndTime = e.ShiftEndTime,
                        TaxNumber = e.TaxNumber,
                        BankName = e.BankName,
                        LeaveBalance = e.LeaveBalance,
                        EmploymentDate = e.EmploymentDate,
                        DoB = e.DoB,
                        IdType = e.IdType,
                        PassportStampDate = e.PassportStampDate,
                        IsBibc = e.IsBibc
                    })
                    .ToListAsync();

                return Ok(employees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employees");
                return StatusCode(500, "An internal server error occurred while retrieving employees.");
            }
        }

        /// <summary>
        /// Retrieves paginated employees using OCC Enterprise Framework standards.
        /// </summary>
        [HttpGet("paged")]
        public async Task<ActionResult<ApiResponse<PagedResult<EmployeeSummaryDto>>>> GetEmployeesPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            try
            {
                var query = _context.Employees.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim().ToLower();
                    query = query.Where(e => e.FirstName.ToLower().Contains(term) || e.LastName.ToLower().Contains(term) || (e.Email != null && e.Email.ToLower().Contains(term)));
                }

                var totalCount = await query.CountAsync();
                var items = await query
                    .OrderBy(e => e.LastName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new EmployeeSummaryDto
                    {
                        Id = e.Id,
                        LinkedUserId = e.LinkedUserId,
                        FirstName = e.FirstName,
                        LastName = e.LastName,
                        IdNumber = e.IdNumber,
                        Email = e.Email,
                        Phone = e.Phone,
                        EmployeeNumber = e.EmployeeNumber,
                        Role = e.Role,
                        Status = e.Status,
                        EmploymentType = e.EmploymentType,
                        Branch = e.Branch,
                        RateType = e.RateType,
                        HourlyRate = e.HourlyRate
                    })
                    .ToListAsync();

                var pagedResult = PagedResult<EmployeeSummaryDto>.Create(items, totalCount, page, pageSize);
                return Ok(ApiResponse<PagedResult<EmployeeSummaryDto>>.Ok(pagedResult, "Employees retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated employees");
                return StatusCode(500, ApiResponse<PagedResult<EmployeeSummaryDto>>.Fail("An internal server error occurred while retrieving employees."));
            }
        }

        /// <summary>
        /// Retrieves detailed information for a specific employee by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the employee.</param>
        /// <returns>The detailed <see cref="EmployeeDto"/> record.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<EmployeeDto>> GetEmployee(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid employee ID.");
            }

            try
            {
                var employee = await _context.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (employee == null)
                {
                    return NotFound("Employee not found.");
                }

                return Ok(ToDetailDto(employee));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employee {Id}", id);
                return StatusCode(500, "An internal server error occurred while retrieving the employee.");
            }
        }

        /// <summary>
        /// Creates a new employee record.
        /// </summary>
        /// <param name="employee">The employee entity to create.</param>
        /// <returns>The created <see cref="EmployeeDto"/> details.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office")]
        public async Task<ActionResult<EmployeeDto>> PostEmployee([FromBody] Employee employee)
        {
            if (employee == null)
            {
                return BadRequest("Employee payload cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(employee.FirstName) || string.IsNullOrWhiteSpace(employee.LastName))
            {
                return BadRequest("First name and Last name are required.");
            }

            // Input Sanitization
            employee.FirstName = employee.FirstName.Trim();
            employee.LastName = employee.LastName.Trim();
            if (!string.IsNullOrEmpty(employee.Email)) employee.Email = employee.Email.Trim();
            if (!string.IsNullOrEmpty(employee.EmployeeNumber)) employee.EmployeeNumber = employee.EmployeeNumber.Trim();

            if (employee.HourlyRate < 0)
            {
                return BadRequest("Hourly rate cannot be negative.");
            }

            try
            {
                if (employee.Id == Guid.Empty)
                {
                    employee.Id = Guid.NewGuid();
                }

                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Employee", "Create", employee.Id);

                return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, ToDetailDto(employee));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee");
                return StatusCode(500, "An internal server error occurred while creating the employee.");
            }
        }

        /// <summary>
        /// Updates an existing employee record.
        /// </summary>
        /// <param name="id">The employee ID matching the route parameter.</param>
        /// <param name="employee">The updated employee entity.</param>
        /// <returns>No content on success.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> PutEmployee(Guid id, [FromBody] Employee employee)
        {
            if (employee == null)
            {
                return BadRequest("Employee payload cannot be null.");
            }

            if (id != employee.Id || id == Guid.Empty)
            {
                return BadRequest("Employee ID mismatch or invalid.");
            }

            if (string.IsNullOrWhiteSpace(employee.FirstName) || string.IsNullOrWhiteSpace(employee.LastName))
            {
                return BadRequest("First name and Last name are required.");
            }

            // Input Sanitization
            employee.FirstName = employee.FirstName.Trim();
            employee.LastName = employee.LastName.Trim();
            if (!string.IsNullOrEmpty(employee.Email)) employee.Email = employee.Email.Trim();
            if (!string.IsNullOrEmpty(employee.EmployeeNumber)) employee.EmployeeNumber = employee.EmployeeNumber.Trim();

            if (employee.HourlyRate < 0)
            {
                return BadRequest("Hourly rate cannot be negative.");
            }

            var existingEmployee = await _context.Employees.FindAsync(id);
            if (existingEmployee == null)
            {
                return NotFound("Employee not found.");
            }

            _context.Entry(existingEmployee).CurrentValues.SetValues(employee);

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Employee", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(id)) return NotFound("Employee no longer exists.");
                return Conflict("Another user has updated this record. Please reload and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee {Id}", id);
                return StatusCode(500, "An internal server error occurred while updating the employee.");
            }

            return NoContent();
        }

        /// <summary>
        /// Deactivates (soft deletes) an employee record after checking active reference constraints.
        /// </summary>
        /// <param name="id">The unique identifier of the employee to deactivate.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> DeleteEmployee(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid employee ID.");
            }

            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee == null)
                {
                    return NotFound("Employee not found.");
                }

                // Safe Deactivation Checks
                if (await _context.TaskAssignments.AnyAsync(ta => ta.AssigneeId == id))
                {
                    return Conflict("Cannot deactivate employee: They are assigned to active tasks.");
                }

                if (await _context.Projects.AnyAsync(p => p.SiteManagerId == id))
                {
                    return Conflict("Cannot deactivate employee: They are listed as Site Manager on a project.");
                }

                if (await _context.TeamMembers.AnyAsync(tm => tm.EmployeeId == id))
                {
                    return Conflict("Cannot deactivate employee: They are currently a member of a team.");
                }

                employee.Status = EmployeeStatus.Inactive;
                _context.Entry(employee).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Employee", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating employee {Id}", id);
                return StatusCode(500, "An internal server error occurred while deactivating the employee.");
            }
        }

        /// <summary>
        /// Counts all database entity references linked to the specified employee.
        /// </summary>
        /// <param name="id">The employee unique identifier.</param>
        /// <returns>An <see cref="EmployeeReferencesDto"/> detailing reference counts.</returns>
        [HttpGet("{id}/references")]
        public async Task<ActionResult<EmployeeReferencesDto>> GetEmployeeReferences(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid employee ID.");
            }

            try
            {
                var dto = new EmployeeReferencesDto
                {
                    AttendanceCount = await _context.AttendanceRecords.CountAsync(a => a.EmployeeId == id),
                    TimeRecordCount = await _context.TimeRecords.CountAsync(t => t.EmployeeId == id),
                    TeamMemberCount = await _context.TeamMembers.CountAsync(t => t.EmployeeId == id),
                    ProjectTeamMemberCount = await _context.ProjectTeamMembers.CountAsync(t => t.EmployeeId == id),
                    SiteDeploymentMemberCount = await _context.SiteDeploymentMembers.CountAsync(s => s.EmployeeId == id),
                    LeaveRequestCount = await _context.LeaveRequests.CountAsync(l => l.EmployeeId == id),
                    OvertimeRequestCount = await _context.OvertimeRequests.CountAsync(o => o.EmployeeId == id),
                    EmployeeLoanCount = await _context.EmployeeLoans.CountAsync(e => e.EmployeeId == id),
                    TaskAssignmentCount = await _context.TaskAssignments.CountAsync(t => t.AssigneeId == id),
                    ClockingEventCount = await _context.ClockingEvents.CountAsync(c => c.EmployeeId == id),
                    DailyTimesheetCount = await _context.DailyTimesheets.CountAsync(d => d.EmployeeId == id),
                    HseqTrainingCount = await _context.HseqTrainingRecords.CountAsync(h => h.EmployeeId == id),
                    WageRunCount = await _context.WageRunLines.CountAsync(w => w.EmployeeId == id),
                    ProjectManagerCount = await _context.Projects.CountAsync(p => p.SiteManagerId == id)
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking references for employee {Id}", id);
                return StatusCode(500, "An internal server error occurred while retrieving employee references.");
            }
        }

        /// <summary>
        /// Permanently removes an employee and clean up or nullifies all foreign-key references. Restricted to Admin role.
        /// </summary>
        /// <param name="id">The unique identifier of the employee to purge.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{id}/permanent")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PermanentDeleteEmployee(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid employee ID.");
            }

            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee == null)
                {
                    return NotFound("Employee not found.");
                }

                // Delete or disassociate references
                if (_context.Database.IsRelational())
                {
                    await _context.Projects.IgnoreQueryFilters().Where(p => p.SiteManagerId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.SiteManagerId, (Guid?)null));
                    await _context.SiteDeployments.IgnoreQueryFilters().Where(s => s.ReceivedBySiteManagerId == id).ExecuteUpdateAsync(s => s.SetProperty(sd => sd.ReceivedBySiteManagerId, (Guid?)null));

                    await _context.TaskAssignments.IgnoreQueryFilters().Where(t => t.AssigneeId == id).ExecuteDeleteAsync();
                    await _context.TeamMembers.IgnoreQueryFilters().Where(t => t.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.ProjectTeamMembers.IgnoreQueryFilters().Where(t => t.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.TimeRecords.IgnoreQueryFilters().Where(t => t.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.AttendanceRecords.IgnoreQueryFilters().Where(t => t.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.LeaveRequests.IgnoreQueryFilters().Where(t => t.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.OvertimeRequests.IgnoreQueryFilters().Where(o => o.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.EmployeeLoans.IgnoreQueryFilters().Where(e => e.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.SiteDeploymentMembers.IgnoreQueryFilters().Where(s => s.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.HseqTrainingRecords.IgnoreQueryFilters().Where(h => h.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.DailyTimesheets.IgnoreQueryFilters().Where(d => d.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.WageRunLines.IgnoreQueryFilters().Where(w => w.EmployeeId == id).ExecuteDeleteAsync();
                    await _context.ClockingEvents.IgnoreQueryFilters().Where(c => c.EmployeeId == id).ExecuteDeleteAsync();
                }
                else
                {
                    var projects = await _context.Projects.IgnoreQueryFilters().Where(p => p.SiteManagerId == id).ToListAsync();
                    foreach (var p in projects) p.SiteManagerId = null;

                    var siteDeps = await _context.SiteDeployments.IgnoreQueryFilters().Where(s => s.ReceivedBySiteManagerId == id).ToListAsync();
                    foreach (var s in siteDeps) s.ReceivedBySiteManagerId = null;

                    _context.TaskAssignments.RemoveRange(await _context.TaskAssignments.IgnoreQueryFilters().Where(t => t.AssigneeId == id).ToListAsync());
                    _context.TeamMembers.RemoveRange(await _context.TeamMembers.IgnoreQueryFilters().Where(t => t.EmployeeId == id).ToListAsync());
                    _context.ProjectTeamMembers.RemoveRange(await _context.ProjectTeamMembers.IgnoreQueryFilters().Where(t => t.EmployeeId == id).ToListAsync());
                    _context.TimeRecords.RemoveRange(await _context.TimeRecords.IgnoreQueryFilters().Where(t => t.EmployeeId == id).ToListAsync());
                    _context.AttendanceRecords.RemoveRange(await _context.AttendanceRecords.IgnoreQueryFilters().Where(t => t.EmployeeId == id).ToListAsync());
                    _context.LeaveRequests.RemoveRange(await _context.LeaveRequests.IgnoreQueryFilters().Where(t => t.EmployeeId == id).ToListAsync());
                    _context.OvertimeRequests.RemoveRange(await _context.OvertimeRequests.IgnoreQueryFilters().Where(o => o.EmployeeId == id).ToListAsync());
                    _context.EmployeeLoans.RemoveRange(await _context.EmployeeLoans.IgnoreQueryFilters().Where(e => e.EmployeeId == id).ToListAsync());
                    _context.SiteDeploymentMembers.RemoveRange(await _context.SiteDeploymentMembers.IgnoreQueryFilters().Where(s => s.EmployeeId == id).ToListAsync());
                    _context.HseqTrainingRecords.RemoveRange(await _context.HseqTrainingRecords.IgnoreQueryFilters().Where(h => h.EmployeeId == id).ToListAsync());
                    _context.DailyTimesheets.RemoveRange(await _context.DailyTimesheets.IgnoreQueryFilters().Where(d => d.EmployeeId == id).ToListAsync());
                    _context.WageRunLines.RemoveRange(await _context.WageRunLines.IgnoreQueryFilters().Where(w => w.EmployeeId == id).ToListAsync());
                    _context.ClockingEvents.RemoveRange(await _context.ClockingEvents.IgnoreQueryFilters().Where(c => c.EmployeeId == id).ToListAsync());
                }

                _context.SupressSoftDelete = true;
                _context.Employees.Remove(employee);

                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Employee", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error permanently deleting employee {Id}", id);
                return StatusCode(500, "An internal server error occurred while permanently deleting the employee.");
            }
        }

        private bool EmployeeExists(Guid id)
        {
            return _context.Employees.Any(e => e.Id == id);
        }

        private static EmployeeDto ToDetailDto(Employee employee)
        {
            return new EmployeeDto
            {
                Id = employee.Id,
                LinkedUserId = employee.LinkedUserId,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                IdNumber = employee.IdNumber,
                IdType = employee.IdType,
                PermitNumber = employee.PermitNumber,
                PassportStampDate = employee.PassportStampDate,
                Email = employee.Email,
                Phone = employee.Phone,
                PhysicalAddress = employee.PhysicalAddress,
                DoB = employee.DoB,
                EmployeeNumber = employee.EmployeeNumber,
                Role = employee.Role,
                Status = employee.Status,
                EmploymentType = employee.EmploymentType,
                ContractDuration = employee.ContractDuration,
                EmploymentDate = employee.EmploymentDate,
                Branch = employee.Branch,
                LivesInCompanyHousing = employee.LivesInCompanyHousing,
                IsBibc = employee.IsBibc,
                ShiftStartTime = employee.ShiftStartTime,
                ShiftEndTime = employee.ShiftEndTime,
                RateType = employee.RateType,
                HourlyRate = employee.HourlyRate,
                TaxNumber = employee.TaxNumber,
                BankName = employee.BankName,
                AccountNumber = employee.AccountNumber,
                BranchCode = employee.BranchCode,
                AccountType = employee.AccountType,
                AnnualLeaveBalance = employee.AnnualLeaveBalance,
                SickLeaveBalance = employee.SickLeaveBalance,
                LeaveBalance = employee.LeaveBalance,
                LeaveCycleStartDate = employee.LeaveCycleStartDate,
                NextOfKinName = employee.NextOfKinName,
                NextOfKinRelation = employee.NextOfKinRelation,
                NextOfKinPhone = employee.NextOfKinPhone,
                EmergencyContactName = employee.EmergencyContactName,
                EmergencyContactPhone = employee.EmergencyContactPhone,
                RowVersion = employee.RowVersion
            };
        }
    }
}
