using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Services;
using OCC.Shared.Models;
using OCC.Shared.DTOs;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for wage run management, payroll draft generation, finalization, line edits, and bank export previews.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WageRunsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWageRunService _wageRunService;
        private readonly ILogger<WageRunsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WageRunsController"/> class.
        /// </summary>
        public WageRunsController(AppDbContext context, IWageRunService wageRunService, ILogger<WageRunsController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _wageRunService = wageRunService ?? throw new ArgumentNullException(nameof(wageRunService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves all wage runs with associated line items ordered descending by start date.
        /// </summary>
        /// <returns>A collection of <see cref="WageRun"/> objects.</returns>
        [HttpGet]
        [Authorize(Roles = "Admin, Office")]
        public async Task<ActionResult<IEnumerable<WageRun>>> GetWageRuns()
        {
            try
            {
                var runs = await _context.WageRuns
                    .Include(w => w.Lines)
                    .OrderByDescending(w => w.StartDate)
                    .ToListAsync();
                return Ok(runs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving wage runs.");
                return StatusCode(500, "An internal server error occurred while retrieving wage runs.");
            }
        }

        /// <summary>
        /// Gets a single wage run by ID including its calculated lines.
        /// </summary>
        /// <param name="id">The unique identifier of the wage run.</param>
        /// <returns>The requested <see cref="WageRun"/>.</returns>
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<ActionResult<WageRun>> GetWageRun(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid wage run ID.");
            }

            try
            {
                var wageRun = await _context.WageRuns
                    .Include(w => w.Lines)
                    .FirstOrDefaultAsync(w => w.Id == id);

                if (wageRun == null)
                {
                    return NotFound("Wage run not found.");
                }

                return Ok(wageRun);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving wage run {Id}", id);
                return StatusCode(500, "An internal server error occurred while retrieving the wage run.");
            }
        }

        /// <summary>
        /// Generates a draft wage run calculation based on dates, branch, and pay type settings.
        /// </summary>
        /// <param name="request">The draft wage run request specification.</param>
        /// <returns>The generated draft <see cref="WageRun"/>.</returns>
        [HttpPost("draft")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<ActionResult<WageRun>> GenerateDraft([FromBody] WageRun request)
        {
            if (request == null)
            {
                return BadRequest("Wage run request payload cannot be null.");
            }

            if (request.StartDate > request.EndDate)
            {
                return BadRequest("Start date cannot be greater than End date.");
            }

            try
            {
                var draftRun = await _wageRunService.GenerateDraftAsync(request);
                return Ok(draftRun);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument during wage run draft generation.");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating draft wage run.");
                var msg = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
                return StatusCode(500, $"An error occurred while generating the draft wage run: {msg}");
            }
        }

        /// <summary>
        /// Finalizes a draft wage run, saving calculations and updating employee loan balances.
        /// </summary>
        /// <param name="run">The wage run entity to finalize.</param>
        /// <returns>The finalized <see cref="WageRun"/> entity.</returns>
        [HttpPost("finalize")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<ActionResult<WageRun>> FinalizeRun([FromBody] WageRun run)
        {
            if (run == null)
            {
                return BadRequest("Invalid Wage Run data payload.");
            }

            if (run.StartDate > run.EndDate)
            {
                return BadRequest("Start date cannot be greater than End date.");
            }

            try
            {
                var finalizedRun = await _wageRunService.FinalizeRunAsync(run);
                return CreatedAtAction(nameof(GetWageRun), new { id = finalizedRun.Id }, finalizedRun);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation exception during wage run finalization.");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing wage run.");
                return StatusCode(500, "An internal server error occurred while finalizing the wage run.");
            }
        }

        /// <summary>
        /// Updates individual line item adjustments (e.g. washing deductions, supervisor incentives) for a draft wage run.
        /// </summary>
        /// <param name="id">The wage run ID.</param>
        /// <param name="updatedLines">The list of updated line adjustments.</param>
        /// <returns>No content on success.</returns>
        [HttpPut("draft/{id}/lines")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> UpdateDraftLines(Guid id, [FromBody] List<WageRunLine> updatedLines)
        {
            if (id == Guid.Empty || updatedLines == null)
            {
                return BadRequest("Invalid request parameters or payload.");
            }

            try
            {
                var run = await _context.WageRuns.Include(w => w.Lines).FirstOrDefaultAsync(w => w.Id == id);
                if (run == null || run.Status != WageRunStatus.Draft)
                {
                    return BadRequest("Run not found or not in Draft state.");
                }

                foreach (var existingLine in run.Lines)
                {
                    var update = updatedLines.FirstOrDefault(l => l.Id == existingLine.Id);
                    if (update != null)
                    {
                        existingLine.DeductionWashing = Math.Max(0m, update.DeductionWashing);
                        existingLine.IncentiveSupervisor = Math.Max(0m, update.IncentiveSupervisor);
                    }
                }

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating draft lines for wage run {Id}", id);
                return StatusCode(500, "An internal server error occurred while updating draft lines.");
            }
        }

        /// <summary>
        /// Exports payment DTO details for bank batch processing for a specific finalized wage run.
        /// </summary>
        /// <param name="id">The wage run ID.</param>
        /// <returns>A list of <see cref="BankPaymentDto"/> records.</returns>
        [HttpGet("{id}/bank-export")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<ActionResult<IEnumerable<BankPaymentDto>>> GetBankExportData(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid wage run ID.");
            }

            try
            {
                var run = await _context.WageRuns
                    .Include(w => w.Lines)
                    .FirstOrDefaultAsync(w => w.Id == id);

                if (run == null)
                {
                    return NotFound("Wage run not found.");
                }

                var payments = new List<BankPaymentDto>();

                foreach (var line in run.Lines)
                {
                    if (line.NetPay <= 0) continue;

                    var employee = await _context.Employees.FindAsync(line.EmployeeId);

                    payments.Add(new BankPaymentDto
                    {
                        EmployeeName = line.EmployeeName,
                        EmployeeNumber = line.EmployeeNumber,
                        BankName = line.BankName ?? employee?.BankName ?? string.Empty,
                        AccountNumber = line.BankAccountNumber ?? employee?.AccountNumber ?? string.Empty,
                        BranchCode = employee?.BranchCode ?? string.Empty,
                        AccountType = employee?.AccountType ?? string.Empty,
                        Amount = line.NetPay,
                        Reference = $"Wage {run.EndDate:yyyyMMdd}"
                    });
                }

                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bank export data for wage run {Id}", id);
                return StatusCode(500, "An internal server error occurred while generating bank export data.");
            }
        }

        /// <summary>
        /// Generates a bank export preview DTO list from a given wage run payload.
        /// </summary>
        /// <param name="run">The wage run payload.</param>
        /// <returns>A list of <see cref="BankPaymentDto"/> records.</returns>
        [HttpPost("bank-export-preview")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<ActionResult<IEnumerable<BankPaymentDto>>> GetBankExportPreview([FromBody] WageRun run)
        {
            if (run == null)
            {
                return BadRequest("Invalid Wage Run data payload.");
            }

            try
            {
                var payments = new List<BankPaymentDto>();

                foreach (var line in run.Lines)
                {
                    if (line.NetPay <= 0) continue;

                    var employee = await _context.Employees.FindAsync(line.EmployeeId);

                    payments.Add(new BankPaymentDto
                    {
                        EmployeeName = line.EmployeeName,
                        EmployeeNumber = line.EmployeeNumber,
                        BankName = line.BankName ?? employee?.BankName ?? string.Empty,
                        AccountNumber = line.BankAccountNumber ?? employee?.AccountNumber ?? string.Empty,
                        BranchCode = employee?.BranchCode ?? string.Empty,
                        AccountType = employee?.AccountType ?? string.Empty,
                        Amount = line.NetPay,
                        Reference = $"Wage {run.EndDate:yyyyMMdd}"
                    });
                }

                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bank export preview.");
                return StatusCode(500, "An internal server error occurred while generating bank export preview.");
            }
        }

        /// <summary>
        /// Deletes an un-finalized draft wage run by ID.
        /// </summary>
        /// <param name="id">The wage run ID to delete.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> DeleteRun(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid wage run ID.");
            }

            try
            {
                var run = await _context.WageRuns.FindAsync(id);
                if (run == null) return NotFound("Wage run not found.");
                
                if (run.Status == WageRunStatus.Finalized) 
                    return BadRequest("Cannot delete a finalized run.");
                    
                _context.WageRuns.Remove(run);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting wage run {Id}", id);
                return StatusCode(500, "An internal server error occurred while deleting the wage run.");
            }
        }
    }
}
