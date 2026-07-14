using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OCC.API.Data;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.API.Hubs;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Allow any authenticated user to READ (Get)
    public class EmployeesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EmployeesController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public EmployeesController(AppDbContext context, ILogger<EmployeesController> logger, IHubContext<Hubs.NotificationHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        // GET: api/Employees
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
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Employees/5
        [HttpGet("{id}")]
        public async Task<ActionResult<EmployeeDto>> GetEmployee(Guid id)
        {
            try
            {
                var employee = await _context.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (employee == null)
                {
                    return NotFound();
                }

                return Ok(ToDetailDto(employee));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employee {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Employees
        [HttpPost]
        [Authorize(Roles = "Admin, Office")] // Admin and Office
        public async Task<ActionResult<EmployeeDto>> PostEmployee(Employee employee)
        {
            try
            {
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Employee", "Create", employee.Id);

                return CreatedAtAction("GetEmployee", new { id = employee.Id }, ToDetailDto(employee));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Employees/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office")] // Admin and Office
        public async Task<IActionResult> PutEmployee(Guid id, Employee employee)
        {
            if (id != employee.Id)
            {
                return BadRequest();
            }

            var existingEmployee = await _context.Employees.FindAsync(id);
            if (existingEmployee == null)
            {
                return NotFound();
            }

            _context.Entry(existingEmployee).CurrentValues.SetValues(employee);

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Employee", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(id)) return NotFound();
                return Conflict("Another user has updated this record. Please reload and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee {Id}", id);
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // DELETE: api/Employees/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office")] // Admin and Office
        public async Task<IActionResult> DeleteEmployee(Guid id)
        {
            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee == null)
                {
                    return NotFound();
                }

                // Safe Deactivation Checks
                
                // 1. Task Assignments
                if (await _context.TaskAssignments.AnyAsync(ta => ta.AssigneeId == id))
                {
                    return Conflict("Cannot deactivate employee: They are assigned to active tasks.");
                }

                // 2. Project Site Manager
                if (await _context.Projects.AnyAsync(p => p.SiteManagerId == id))
                {
                    return Conflict("Cannot deactivate employee: They are listed as Site Manager on a project.");
                }

                // 3. Team Membership
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
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Employees/5/references
        [HttpGet("{id}/references")]
        public async Task<ActionResult<EmployeeReferencesDto>> GetEmployeeReferences(Guid id)
        {
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
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Employees/5/permanent
        [HttpDelete("{id}/permanent")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PermanentDeleteEmployee(Guid id)
        {
            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee == null)
                {
                    return NotFound();
                }

                // Delete or disassociate references (using direct SQL commands to bypass column mismatch schema checks in EF tracking):
                
                // 1. Projects Site Manager - nullify
                await _context.Projects.IgnoreQueryFilters().Where(p => p.SiteManagerId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.SiteManagerId, (Guid?)null));

                // 2. SiteDeployments site manager - nullify
                await _context.SiteDeployments.IgnoreQueryFilters().Where(s => s.ReceivedBySiteManagerId == id).ExecuteUpdateAsync(s => s.SetProperty(sd => sd.ReceivedBySiteManagerId, (Guid?)null));

                // 3. Delete other direct references:
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

                // 4. Suppress Soft Delete on EF DBContext and delete employee record
                _context.SupressSoftDelete = true;
                _context.Employees.Remove(employee);

                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Employee", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error permanently deleting employee {Id}", id);
                return StatusCode(500, ex.Message);
            }
        }

        private bool EmployeeExists(Guid id)
        {
            return _context.Employees.Any(e => e.Id == id);
        }

        private EmployeeSummaryDto ToSummaryDto(Employee employee)
        {
            return new EmployeeSummaryDto
            {
                Id = employee.Id,
                LinkedUserId = employee.LinkedUserId,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                IdNumber = employee.IdNumber,
                Email = employee.Email,
                Phone = employee.Phone,
                EmployeeNumber = employee.EmployeeNumber,
                Role = employee.Role,
                Status = employee.Status,
                EmploymentType = employee.EmploymentType,
                Branch = employee.Branch,
                RateType = employee.RateType,
                HourlyRate = employee.HourlyRate,
                ShiftStartTime = employee.ShiftStartTime,
                ShiftEndTime = employee.ShiftEndTime,
                TaxNumber = employee.TaxNumber,
                BankName = employee.BankName,
                LeaveBalance = employee.LeaveBalance,
                EmploymentDate = employee.EmploymentDate,
                DoB = employee.DoB,
                IdType = employee.IdType,
                PassportStampDate = employee.PassportStampDate,
                IsBibc = employee.IsBibc
            };
        }

        private EmployeeDto ToDetailDto(Employee employee)
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
