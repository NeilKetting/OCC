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
    public class SnagJobsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SnagJobsController> _logger;

        public SnagJobsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<SnagJobsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/SnagJobs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SnagJob>>> GetSnagJobs()
        {
            try
            {
                return await _context.SnagJobs
                    .Include(s => s.Project)
                    .Include(s => s.SubContractor)
                    .Include(s => s.OriginalTask)
                    .OrderByDescending(s => s.CreatedAtUtc)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving snag jobs");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/SnagJobs/project/5
        [HttpGet("project/{projectId}")]
        public async Task<ActionResult<IEnumerable<SnagJob>>> GetProjectSnagJobs(Guid projectId)
        {
            try
            {
                return await _context.SnagJobs
                    .Where(s => s.ProjectId == projectId)
                    .Include(s => s.SubContractor)
                    .Include(s => s.OriginalTask)
                    .OrderByDescending(s => s.CreatedAtUtc)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving snag jobs for project {ProjectId}", projectId);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/SnagJobs/subcontractor/5
        [HttpGet("subcontractor/{subContractorId}")]
        public async Task<ActionResult<IEnumerable<SnagJob>>> GetSubContractorSnagJobs(Guid subContractorId)
        {
            try
            {
                return await _context.SnagJobs
                    .Where(s => s.SubContractorId == subContractorId)
                    .Include(s => s.Project)
                    .Include(s => s.OriginalTask)
                    .OrderByDescending(s => s.CreatedAtUtc)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving snag jobs for sub-contractor {SubContractorId}", subContractorId);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/SnagJobs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SnagJob>> GetSnagJob(Guid id)
        {
            try
            {
                var snagJob = await _context.SnagJobs
                    .Include(s => s.Project)
                    .Include(s => s.SubContractor)
                    .Include(s => s.OriginalTask)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (snagJob == null) return NotFound();
                return snagJob;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving snag job {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/SnagJobs
        [HttpPost]
        public async Task<ActionResult<SnagJob>> PostSnagJob(SnagJob snagJob)
        {
            try
            {
                if (snagJob.Id == Guid.Empty) snagJob.Id = Guid.NewGuid();
                
                _context.SnagJobs.Add(snagJob);
                await _context.SaveChangesAsync();
                
                // Track creation in sub-contractor stats
                await RecalculateRating(snagJob.SubContractorId);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SnagJob", "Create", snagJob.Id);

                return CreatedAtAction("GetSnagJob", new { id = snagJob.Id }, snagJob);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating snag job");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/SnagJobs/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSnagJob(Guid id, SnagJob snagJob)
        {
            if (id != snagJob.Id) return BadRequest();

            try
            {
                var originalSnag = await _context.SnagJobs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
                if (originalSnag == null) return NotFound();

                // Check if status changed
                bool statusChanged = snagJob.Status != originalSnag.Status;
                if (statusChanged && (snagJob.Status == SnagStatus.Fixed || snagJob.Status == SnagStatus.Verified || snagJob.Status == SnagStatus.Closed))
                {
                    if (!snagJob.CompletionDate.HasValue) snagJob.CompletionDate = DateTime.UtcNow;
                }

                _context.Update(snagJob);
                await _context.SaveChangesAsync();
                
                if (statusChanged)
                {
                    await RecalculateRating(snagJob.SubContractorId);
                    await _context.SaveChangesAsync();
                }

                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SnagJob", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SnagJobExists(id)) return NotFound();
                return Conflict("Another user has updated this record. Please reload and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating snag job {Id}", id);
                return StatusCode(500, "Internal server error");
            }
            return NoContent();
        }

        // DELETE: api/SnagJobs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSnagJob(Guid id)
        {
            try
            {
                var snagJob = await _context.SnagJobs.FindAsync(id);
                if (snagJob == null) return NotFound();

                var subContractorId = snagJob.SubContractorId;
                _context.SnagJobs.Remove(snagJob);
                await _context.SaveChangesAsync();
                
                await RecalculateRating(subContractorId);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SnagJob", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting snag job {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task RecalculateRating(Guid subContractorId)
        {
            try
            {
                var contractor = await _context.SubContractors.FindAsync(subContractorId);
                if (contractor == null) return;

                var snags = await _context.SnagJobs
                    .Where(s => s.SubContractorId == subContractorId)
                    .AsNoTracking()
                    .ToListAsync();

                int activeSnags = snags.Count(s => s.Status == SnagStatus.Open || s.Status == SnagStatus.InProgress);
                int totalSnags = snags.Count;
                
                // Rating Calculation Logic:
                // 1. Base rating is OnTimeRate * 5.0 (max 5.0)
                // 2. Deduction for active snags: -0.3 per active snag
                // 3. Deduction for historical snags: -0.05 per resolved snag (minor quality stain)
                
                decimal baseRating = contractor.OnTimeRate * 5.0m;
                if (contractor.CompletedTasksCount == 0 && totalSnags == 0) baseRating = 5.0m;
                else if (contractor.CompletedTasksCount == 0 && totalSnags > 0) baseRating = 3.0m;
                
                decimal activeDeduction = activeSnags * 0.3m;
                
                // Dilution Formula: Historical snags are penalized based on their ratio to total completed tasks.
                // This allows partners to "work off" a bad rating by performing well over time.
                decimal resolvedSnags = totalSnags - activeSnags;
                decimal snagRatio = contractor.CompletedTasksCount > 0 
                    ? resolvedSnags / contractor.CompletedTasksCount 
                    : resolvedSnags > 0 ? 0.5m : 0m;
                
                decimal historicalDeduction = Math.Min(snagRatio * 1.5m, 1.5m);
                
                decimal finalRating = baseRating - activeDeduction - historicalDeduction;
                
                contractor.Rating = Math.Max(1.0m, Math.Min(5.0m, finalRating));
                contractor.TotalSnagsCount = totalSnags;

                // Set Tier
                contractor.PerformanceTier = contractor.Rating switch
                {
                    >= 4.8m => "Diamond",
                    >= 4.0m => "Gold",
                    >= 3.0m => "Silver",
                    _ => "Bronze"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating rating for subcontractor {Id}", subContractorId);
            }
        }

        private bool SnagJobExists(Guid id) => _context.SnagJobs.Any(e => e.Id == id);
    }
}
