using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OCC.API.Data;
using OCC.Shared.Models;
using OCC.API.Hubs;
using OCC.Shared.DTOs;

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

            var existingLoan = await _context.EmployeeLoans.FindAsync(id);
            if (existingLoan == null)
            {
                return NotFound();
            }

            _context.Entry(existingLoan).CurrentValues.SetValues(loan);

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

        [HttpGet("{id}/statement")]
        public async Task<ActionResult<LoanStatementDto>> GetLoanStatement(Guid id)
        {
            try
            {
                var loan = await _context.EmployeeLoans
                    .Include(l => l.Employee)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (loan == null) return NotFound();

                // Find all finalized/paid wage run lines for this employee since the loan's start date
                var wageRunLines = await _context.WageRunLines
                    .Include(w => w.WageRun)
                    .Where(w => w.EmployeeId == loan.EmployeeId && 
                                w.DeductionLoan > 0 && 
                                w.WageRun != null &&
                                (w.WageRun.Status == WageRunStatus.Finalized || w.WageRun.Status == WageRunStatus.Paid) &&
                                w.WageRun.EndDate >= loan.StartDate)
                    .OrderBy(w => w.WageRun!.RunDate)
                    .ToListAsync();

                var statement = new LoanStatementDto
                {
                    LoanId = loan.Id,
                    EmployeeName = $"{loan.Employee?.FirstName} {loan.Employee?.LastName}",
                    EmployeeNumber = loan.Employee?.EmployeeNumber ?? string.Empty,
                    PrincipalAmount = loan.PrincipalAmount,
                    InterestRate = loan.InterestRate,
                    MonthlyInstallment = loan.MonthlyInstallment,
                    OutstandingBalance = loan.OutstandingBalance,
                    StartDate = loan.StartDate,
                    Payments = new List<LoanStatementPaymentDto>()
                };

                decimal currentBalance = loan.PrincipalAmount + (loan.PrincipalAmount * loan.InterestRate / 100);
                foreach (var line in wageRunLines)
                {
                    if (line.WageRun == null) continue;

                    currentBalance -= line.DeductionLoan;
                    if (currentBalance < 0) currentBalance = 0;

                    statement.Payments.Add(new LoanStatementPaymentDto
                    {
                        Date = line.WageRun.RunDate,
                        Amount = line.DeductionLoan,
                        Notes = $"Deducted in Wage Run ({line.WageRun.StartDate:dd MMM} - {line.WageRun.EndDate:dd MMM yyyy})",
                        BalanceAfterPayment = currentBalance
                    });
                }

                return Ok(statement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating loan statement for loan {LoanId}", id);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
