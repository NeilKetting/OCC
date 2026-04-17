using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OCC.API.Data;
using OCC.Shared.Models;
using OCC.API.Hubs;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmployeeLoansController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EmployeeLoansController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public EmployeeLoansController(AppDbContext context, ILogger<EmployeeLoansController> logger, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        // GET: api/EmployeeLoans
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EmployeeLoan>>> GetEmployeeLoans()
        {
            try
            {
                return await _context.EmployeeLoans
                    .Include(l => l.Employee)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employee loans");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/EmployeeLoans/active
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<EmployeeLoan>>> GetActiveLoans()
        {
            try
            {
                return await _context.EmployeeLoans
                    .Include(l => l.Employee)
                    .Where(l => l.IsActive && l.OutstandingBalance > 0)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active loans");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/EmployeeLoans/5
        [HttpGet("{id}")]
        public async Task<ActionResult<EmployeeLoan>> GetEmployeeLoan(Guid id)
        {
            var loan = await _context.EmployeeLoans
                .Include(l => l.Employee)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
            {
                return NotFound();
            }

            return loan;
        }

        // POST: api/EmployeeLoans
        [HttpPost]
        [Authorize(Roles = "Admin, Office")]
        public async Task<ActionResult<EmployeeLoan>> PostEmployeeLoan(EmployeeLoan loan)
        {
            try
            {
                // Basic validation
                if(loan.EmployeeId == Guid.Empty)
                    return BadRequest("Employee must be selected.");

                _context.EmployeeLoans.Add(loan);
                await _context.SaveChangesAsync();

                // Notify clients
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "EmployeeLoan", "Create", loan.Id);

                return CreatedAtAction("GetEmployeeLoan", new { id = loan.Id }, loan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee loan");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/EmployeeLoans/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> PutEmployeeLoan(Guid id, EmployeeLoan loan)
        {
            if (id != loan.Id)
            {
                return BadRequest();
            }

            _context.Entry(loan).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "EmployeeLoan", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeLoanExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee loan {Id}", id);
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // DELETE: api/EmployeeLoans/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> DeleteEmployeeLoan(Guid id)
        {
            var loan = await _context.EmployeeLoans.FindAsync(id);
            if (loan == null)
            {
                return NotFound();
            }

            // Soft delete
            loan.IsActive = false; 
             // Logic for handling outstanding balance on delete? 
             // Usually specialized logic needed, but for now generic soft delete.
             
            _context.Entry(loan).State = EntityState.Modified;

            try 
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "EmployeeLoan", "Delete", id);
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error deleting employee loan {Id}", id);
                 return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        private bool EmployeeLoanExists(Guid id)
        {
            return _context.EmployeeLoans.Any(e => e.Id == id);
        }
    }
}
