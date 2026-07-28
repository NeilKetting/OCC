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
    /// API Controller for managing snag jobs (defects/remedial tasks) and calculating subcontractor performance ratings.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SnagJobsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SnagJobsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SnagJobsController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="hubContext">SignalR notification hub context.</param>
        /// <param name="logger">Logger instance.</param>
        public SnagJobsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<SnagJobsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all snag jobs across projects.
        /// </summary>
        /// <returns>A list of snag jobs.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SnagJob>>> GetSnagJobs()
        {
            try
            {
                var snagJobs = await _context.SnagJobs
                    .Include(s => s.Project)
                    .Include(s => s.SubContractor)
                    .Include(s => s.OriginalTask)
                    .OrderByDescending(s => s.CreatedAtUtc)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(snagJobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving snag jobs");
                return StatusCode(500, "An error occurred while retrieving snag jobs.");
            }
        }

        /// <summary>
        /// Retrieves snag jobs associated with a specific project.
        /// </summary>
        /// <param name="projectId">The project ID.</param>
        /// <returns>A list of snag jobs for the project.</returns>
        [HttpGet("project/{projectId}")]
        public async Task<ActionResult<IEnumerable<SnagJob>>> GetProjectSnagJobs(Guid projectId)
        {
            if (projectId == Guid.Empty) return BadRequest("Invalid project ID.");

            try
            {
                var snagJobs = await _context.SnagJobs
                    .Where(s => s.ProjectId == projectId)
                    .Include(s => s.SubContractor)
                    .Include(s => s.OriginalTask)
                    .OrderByDescending(s => s.CreatedAtUtc)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(snagJobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving snag jobs for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while retrieving project snag jobs.");
            }
        }

        /// <summary>
        /// Retrieves snag jobs assigned to a specific subcontractor.
        /// </summary>
        /// <param name="subContractorId">The subcontractor ID.</param>
        /// <returns>A list of snag jobs for the subcontractor.</returns>
        [HttpGet("subcontractor/{subContractorId}")]
        public async Task<ActionResult<IEnumerable<SnagJob>>> GetSubContractorSnagJobs(Guid subContractorId)
        {
            if (subContractorId == Guid.Empty) return BadRequest("Invalid subcontractor ID.");

            try
            {
                var snagJobs = await _context.SnagJobs
                    .Where(s => s.SubContractorId == subContractorId)
                    .Include(s => s.Project)
                    .Include(s => s.OriginalTask)
                    .OrderByDescending(s => s.CreatedAtUtc)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(snagJobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving snag jobs for sub-contractor {SubContractorId}", subContractorId);
                return StatusCode(500, "An error occurred while retrieving subcontractor snag jobs.");
            }
        }

        /// <summary>
        /// Retrieves a single snag job by its unique identifier.
        /// </summary>
        /// <param name="id">The snag job ID.</param>
        /// <returns>The requested snag job entity.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<SnagJob>> GetSnagJob(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid snag job ID.");

            try
            {
                var snagJob = await _context.SnagJobs
                    .Include(s => s.Project)
                    .Include(s => s.SubContractor)
                    .Include(s => s.OriginalTask)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (snagJob == null) return NotFound();
                return Ok(snagJob);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving snag job {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the snag job.");
            }
        }

        /// <summary>
        /// Creates a new snag job and updates subcontractor rating statistics.
        /// </summary>
        /// <param name="snagJob">The snag job entity.</param>
        /// <returns>The created snag job entity.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<ActionResult<SnagJob>> PostSnagJob(SnagJob snagJob)
        {
            if (snagJob == null) return BadRequest("Snag job payload cannot be null.");
            if (snagJob.ProjectId == Guid.Empty) return BadRequest("Invalid project ID.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                if (snagJob.Id == Guid.Empty) snagJob.Id = Guid.NewGuid();
                
                snagJob.Title = InputSanitizer.Sanitize(snagJob.Title);
                snagJob.Description = InputSanitizer.Sanitize(snagJob.Description);

                _context.SnagJobs.Add(snagJob);
                await _context.SaveChangesAsync();
                
                await RecalculateRating(snagJob.SubContractorId);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "SnagJob", "Create", snagJob.Id);

                return CreatedAtAction("GetSnagJob", new { id = snagJob.Id }, snagJob);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating snag job");
                return StatusCode(500, "An error occurred while creating the snag job.");
            }
        }

        /// <summary>
        /// Updates an existing snag job entity and recalculates subcontractor performance ratings if status changes.
        /// </summary>
        /// <param name="id">The snag job ID.</param>
        /// <param name="snagJob">The updated snag job entity.</param>
        /// <returns>No content on success.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> PutSnagJob(Guid id, SnagJob snagJob)
        {
            if (id == Guid.Empty || id != snagJob.Id) return BadRequest("Snag job ID mismatch or empty.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var existingSnag = await _context.SnagJobs.FirstOrDefaultAsync(s => s.Id == id);
                if (existingSnag == null) return NotFound();

                snagJob.Title = InputSanitizer.Sanitize(snagJob.Title);
                snagJob.Description = InputSanitizer.Sanitize(snagJob.Description);

                bool statusChanged = snagJob.Status != existingSnag.Status;
                if (statusChanged && (snagJob.Status == SnagStatus.Fixed || snagJob.Status == SnagStatus.Verified || snagJob.Status == SnagStatus.Closed))
                {
                    if (!snagJob.CompletionDate.HasValue) snagJob.CompletionDate = DateTime.UtcNow;
                }

                _context.Entry(existingSnag).CurrentValues.SetValues(snagJob);
                await _context.SaveChangesAsync();
                
                if (statusChanged)
                {
                    await RecalculateRating(existingSnag.SubContractorId);
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
                return StatusCode(500, "An error occurred while updating the snag job.");
            }
            return NoContent();
        }

        /// <summary>
        /// Deletes a snag job by its unique identifier.
        /// </summary>
        /// <param name="id">The snag job ID.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> DeleteSnagJob(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid snag job ID.");

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
                return StatusCode(500, "An error occurred while deleting the snag job.");
            }
        }

        private async Task RecalculateRating(Guid subContractorId)
        {
            if (subContractorId == Guid.Empty) return;

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
                
                decimal baseRating = contractor.OnTimeRate * 5.0m;
                if (contractor.CompletedTasksCount == 0 && totalSnags == 0) baseRating = 5.0m;
                else if (contractor.CompletedTasksCount == 0 && totalSnags > 0) baseRating = 3.0m;
                
                decimal activeDeduction = activeSnags * 0.3m;
                
                decimal resolvedSnags = totalSnags - activeSnags;
                decimal snagRatio = contractor.CompletedTasksCount > 0 
                    ? resolvedSnags / contractor.CompletedTasksCount 
                    : resolvedSnags > 0 ? 0.5m : 0m;
                
                decimal historicalDeduction = Math.Min(snagRatio * 1.5m, 1.5m);
                
                decimal finalRating = baseRating - activeDeduction - historicalDeduction;
                
                contractor.Rating = Math.Max(1.0m, Math.Min(5.0m, finalRating));
                contractor.TotalSnagsCount = totalSnags;

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
