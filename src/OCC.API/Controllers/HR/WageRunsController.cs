using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Services;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WageRunsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWageRunService _wageRunService;

        public WageRunsController(AppDbContext context, IWageRunService wageRunService)
        {
            _context = context;
            _wageRunService = wageRunService;
        }

        // GET: api/WageRuns
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WageRun>>> GetWageRuns()
        {
            return await _context.WageRuns
                .Include(w => w.Lines)
                .OrderByDescending(w => w.StartDate)
                .ToListAsync();
        }

        // GET: api/WageRuns/5
        [HttpGet("{id}")]
        public async Task<ActionResult<WageRun>> GetWageRun(Guid id)
        {
            var wageRun = await _context.WageRuns
                .Include(w => w.Lines)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (wageRun == null)
            {
                return NotFound();
            }

            return wageRun;
        }

        // POST: api/WageRuns/draft
        [HttpPost("draft")]
        public async Task<ActionResult<WageRun>> GenerateDraft([FromBody] WageRun request)
        {
            try
            {
                var draftRun = await _wageRunService.GenerateDraftAsync(request);
                return Ok(draftRun);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/WageRuns/finalize
        [HttpPost("finalize")]
        public async Task<ActionResult<WageRun>> FinalizeRun([FromBody] WageRun run)
        {
            if (run == null) return BadRequest("Invalid Wage Run data.");

            try
            {
                var finalizedRun = await _wageRunService.FinalizeRunAsync(run);
                return CreatedAtAction("GetWageRun", new { id = finalizedRun.Id }, finalizedRun);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: api/WageRuns/draft/{id}/lines
        [HttpPut("draft/{id}/lines")]
        public async Task<IActionResult> UpdateDraftLines(Guid id, [FromBody] List<WageRunLine> updatedLines)
        {
            var run = await _context.WageRuns.Include(w => w.Lines).FirstOrDefaultAsync(w => w.Id == id);
            if (run == null || run.Status != WageRunStatus.Draft) return BadRequest("Run not found or not in Draft state.");

            foreach (var existingLine in run.Lines)
            {
                var update = updatedLines.FirstOrDefault(l => l.Id == existingLine.Id);
                if (update != null)
                {
                    existingLine.DeductionWashing = update.DeductionWashing;
                    existingLine.IncentiveSupervisor = update.IncentiveSupervisor;
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET: api/WageRuns/{id}/bank-export
        [HttpGet("{id}/bank-export")]
        public async Task<ActionResult<IEnumerable<BankPaymentDto>>> GetBankExportData(Guid id)
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

            return payments;
        }

        // POST: api/WageRuns/bank-export-preview
        [HttpPost("bank-export-preview")]
        public async Task<ActionResult<IEnumerable<BankPaymentDto>>> GetBankExportPreview([FromBody] WageRun run)
        {
            if (run == null)
            {
                return BadRequest("Invalid Wage Run data.");
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

            return payments;
        }

        // DELETE: api/WageRuns/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRun(Guid id)
        {
             var run = await _context.WageRuns.FindAsync(id);
             if (run == null) return NotFound();
             
             if (run.Status == WageRunStatus.Finalized) 
                 return BadRequest("Cannot delete a finalized run.");
                 
             _context.WageRuns.Remove(run);
             await _context.SaveChangesAsync();
             return NoContent();
        }
    }
}
