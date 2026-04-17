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
                        EmploymentDate = e.EmploymentDate
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

            _context.Entry(employee).State = EntityState.Modified;

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

                // Safe Deletion Checks
                
                // 1. Task Assignments
                if (await _context.TaskAssignments.AnyAsync(ta => ta.AssigneeId == id))
                {
                    return Conflict("Cannot delete employee: They are assigned to active tasks.");
                }

                // 2. Project Site Manager
                if (await _context.Projects.AnyAsync(p => p.SiteManagerId == id))
                {
                    return Conflict("Cannot delete employee: They are listed as Site Manager on a project.");
                }

                // 3. Team Membership
                if (await _context.TeamMembers.AnyAsync(tm => tm.EmployeeId == id))
                {
                    return Conflict("Cannot delete employee: They are currently a member of a team.");
                }

                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Employee", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee {Id}", id);
                return StatusCode(500, "Internal server error");
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
                EmploymentDate = employee.EmploymentDate
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
