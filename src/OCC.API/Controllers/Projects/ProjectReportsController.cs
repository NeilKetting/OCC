using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Security;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for managing project report drafts, weekly report history, and secure photo/report file uploads.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProjectReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProjectReportsController> _logger;

        private static readonly string[] AllowedPhotoExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedReportExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".png", ".jpg", ".jpeg" };

        private const long MaxPhotoSizeBytes = 10 * 1024 * 1024; // 10 MB
        private const long MaxReportSizeBytes = 25 * 1024 * 1024; // 25 MB

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectReportsController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="logger">Logger instance.</param>
        public ProjectReportsController(AppDbContext context, ILogger<ProjectReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the report draft for a specified project, creating an initial default draft if one does not exist.
        /// </summary>
        /// <param name="projectId">The project ID.</param>
        /// <returns>The report draft for the project.</returns>
        [HttpGet("draft/{projectId}")]
        public async Task<ActionResult<ProjectReportDraft>> GetDraft(Guid projectId)
        {
            if (projectId == Guid.Empty) return BadRequest("Invalid project ID.");

            try
            {
                var draft = await _context.ProjectReportDrafts
                    .FirstOrDefaultAsync(d => d.ProjectId == projectId);

                if (draft == null)
                {
                    var project = await _context.Projects.FindAsync(projectId);
                    if (project == null)
                    {
                        return NotFound("Project not found.");
                    }

                    draft = new ProjectReportDraft
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        StatusSummary = project.Description ?? string.Empty,
                        GeneralWasteTon = "0",
                        RubbleM3 = "0",
                        ScrapMetalsTon = "0",
                        AsbestosTon = "0",
                        SiteEstablishmentPlanned = project.StartDate,
                        PracticalCompletionPlanned = project.EndDate,
                        StreamingPlanned = project.EndDate,
                        CreatedAtUtc = DateTime.UtcNow,
                        CreatedBy = "System",
                        IsActive = true
                    };
                }

                return Ok(draft);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving draft report for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while retrieving the report draft.");
            }
        }

        /// <summary>
        /// Saves or updates the report draft for a project.
        /// </summary>
        /// <param name="projectId">The project ID.</param>
        /// <param name="draft">The report draft payload.</param>
        /// <returns>No content on success.</returns>
        [HttpPut("draft/{projectId}")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<IActionResult> SaveDraft(Guid projectId, [FromBody] ProjectReportDraft draft)
        {
            if (projectId == Guid.Empty || draft == null || projectId != draft.ProjectId)
            {
                return BadRequest("Project ID mismatch or empty.");
            }
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                // Sanitize draft input strings
                draft.StatusSummary = InputSanitizer.Sanitize(draft.StatusSummary);
                draft.GeneralWasteTon = InputSanitizer.Sanitize(draft.GeneralWasteTon);
                draft.RubbleM3 = InputSanitizer.Sanitize(draft.RubbleM3);
                draft.ScrapMetalsTon = InputSanitizer.Sanitize(draft.ScrapMetalsTon);
                draft.AsbestosTon = InputSanitizer.Sanitize(draft.AsbestosTon);
                draft.OverdueMilestoneReasons = InputSanitizer.Sanitize(draft.OverdueMilestoneReasons);

                var existing = await _context.ProjectReportDrafts
                    .FirstOrDefaultAsync(d => d.ProjectId == projectId);

                if (existing == null)
                {
                    draft.Id = Guid.NewGuid();
                    _context.ProjectReportDrafts.Add(draft);
                }
                else
                {
                    existing.StatusSummary = draft.StatusSummary;
                    existing.GeneralWasteTon = draft.GeneralWasteTon;
                    existing.RubbleM3 = draft.RubbleM3;
                    existing.ScrapMetalsTon = draft.ScrapMetalsTon;
                    existing.AsbestosTon = draft.AsbestosTon;
                    existing.SiteEstablishmentPlanned = draft.SiteEstablishmentPlanned;
                    existing.SiteEstablishmentActual = draft.SiteEstablishmentActual;
                    existing.PracticalCompletionPlanned = draft.PracticalCompletionPlanned;
                    existing.PracticalCompletionActual = draft.PracticalCompletionActual;
                    existing.StreamingPlanned = draft.StreamingPlanned;
                    existing.StreamingActual = draft.StreamingActual;
                    existing.PowPercentRequired = draft.PowPercentRequired;
                    existing.DelayDays = draft.DelayDays;
                    existing.OverdueMilestoneReasons = draft.OverdueMilestoneReasons;
                    existing.PhotoUrls = draft.PhotoUrls;
                }

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving draft report for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while saving the report draft.");
            }
        }

        /// <summary>
        /// Uploads a photo attachment for inclusion in a project report draft.
        /// Performs OWASP-compliant file validation (file size, extension whitelist, path traversal prevention).
        /// </summary>
        /// <param name="file">The uploaded image file.</param>
        /// <returns>Relative URL of the saved photo.</returns>
        [HttpPost("draft/upload-photo")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<ActionResult<string>> UploadReportPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            if (file.Length > MaxPhotoSizeBytes)
            {
                return BadRequest("File size exceeds maximum allowed limit of 10MB.");
            }

            var originalFileName = Path.GetFileName(file.FileName);
            if (!InputSanitizer.IsAllowedExtension(originalFileName, AllowedPhotoExtensions))
            {
                return BadRequest("Invalid photo file extension. Allowed extensions: .jpg, .jpeg, .png, .webp");
            }

            try
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "project_reports");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var photoId = Guid.NewGuid();
                var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
                var fileName = $"{photoId}{ext}";
                var filePath = Path.GetFullPath(Path.Combine(uploadsFolder, fileName));

                // Path traversal check
                if (!filePath.StartsWith(Path.GetFullPath(uploadsFolder), StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Invalid file path.");
                }

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/uploads/project_reports/{fileName}";
                return Ok(new { url = relativePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading report photo");
                return StatusCode(500, "An error occurred while uploading the photo.");
            }
        }

        /// <summary>
        /// Retrieves the list of historical generated reports for a specific project.
        /// </summary>
        /// <param name="projectId">The project ID.</param>
        /// <returns>A list of report history records.</returns>
        [HttpGet("history/{projectId}")]
        public async Task<ActionResult<IEnumerable<ProjectReportHistory>>> GetHistory(Guid projectId)
        {
            if (projectId == Guid.Empty) return BadRequest("Invalid project ID.");

            try
            {
                var history = await _context.ProjectReportHistories
                    .Where(h => h.ProjectId == projectId)
                    .OrderByDescending(h => h.GeneratedDate)
                    .ToListAsync();

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report history for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while retrieving report history.");
            }
        }

        /// <summary>
        /// Uploads a generated PDF/document project report into historical records.
        /// Performs OWASP file extension whitelist, size validation, and path traversal prevention.
        /// </summary>
        /// <param name="projectId">The project ID.</param>
        /// <param name="reportName">Display name for the report.</param>
        /// <param name="weekNumber">Week number of the report.</param>
        /// <param name="file">The uploaded report document file.</param>
        /// <returns>The created project report history record.</returns>
        [HttpPost("history")]
        [Authorize(Roles = "Admin, Office, SiteManager")]
        public async Task<ActionResult<ProjectReportHistory>> UploadReport(
            [FromForm] Guid projectId,
            [FromForm] string reportName,
            [FromForm] int weekNumber,
            IFormFile file)
        {
            if (projectId == Guid.Empty) return BadRequest("Invalid project ID.");
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            if (file.Length > MaxReportSizeBytes)
            {
                return BadRequest("File size exceeds maximum allowed limit of 25MB.");
            }

            var originalFileName = Path.GetFileName(file.FileName);
            if (!InputSanitizer.IsAllowedExtension(originalFileName, AllowedReportExtensions))
            {
                return BadRequest("Invalid report file extension. Allowed extensions: .pdf, .doc, .docx, .xls, .xlsx, .csv, .png, .jpg, .jpeg");
            }

            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null)
                {
                    return NotFound("Project not found.");
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "project_reports");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var historyId = Guid.NewGuid();
                var safeExt = Path.GetExtension(originalFileName).ToLowerInvariant();
                var fileName = $"{historyId}_{Guid.NewGuid().ToString("N")[..8]}{safeExt}";
                var filePath = Path.GetFullPath(Path.Combine(uploadsFolder, fileName));

                if (!filePath.StartsWith(Path.GetFullPath(uploadsFolder), StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Invalid file path.");
                }

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var history = new ProjectReportHistory
                {
                    Id = historyId,
                    ProjectId = projectId,
                    ReportName = InputSanitizer.Sanitize(reportName),
                    WeekNumber = weekNumber,
                    FilePath = $"/uploads/project_reports/{fileName}",
                    FileSize = FormatFileSize(file.Length),
                    GeneratedDate = DateTime.UtcNow,
                    GeneratedBy = User.Identity?.Name ?? "System",
                    IsActive = true
                };

                _context.ProjectReportHistories.Add(history);
                await _context.SaveChangesAsync();

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading report document for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while uploading the report.");
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
