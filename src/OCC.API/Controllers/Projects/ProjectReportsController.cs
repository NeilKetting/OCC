using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectReportsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("draft/{projectId}")]
        public async Task<ActionResult<ProjectReportDraft>> GetDraft(Guid projectId)
        {
            var draft = await _context.ProjectReportDrafts
                .FirstOrDefaultAsync(d => d.ProjectId == projectId);

            if (draft == null)
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null)
                {
                    return NotFound("Project not found");
                }

                // Return a draft initialized with project defaults
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

        [HttpPut("draft/{projectId}")]
        public async Task<IActionResult> SaveDraft(Guid projectId, [FromBody] ProjectReportDraft draft)
        {
            if (projectId != draft.ProjectId)
            {
                return BadRequest("Project ID mismatch");
            }

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

        [HttpPost("draft/upload-photo")]
        public async Task<ActionResult<string>> UploadReportPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "project_reports");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var photoId = Guid.NewGuid();
            var fileName = $"{photoId}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/uploads/project_reports/{fileName}";
            return Ok(new { url = relativePath });
        }

        [HttpGet("history/{projectId}")]
        public async Task<ActionResult<IEnumerable<ProjectReportHistory>>> GetHistory(Guid projectId)
        {
            var history = await _context.ProjectReportHistories
                .Where(h => h.ProjectId == projectId)
                .OrderByDescending(h => h.GeneratedDate)
                .ToListAsync();

            return Ok(history);
        }

        [HttpPost("history")]
        public async Task<ActionResult<ProjectReportHistory>> UploadReport(
            [FromForm] Guid projectId,
            [FromForm] string reportName,
            [FromForm] int weekNumber,
            IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound("Project not found");
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "project_reports");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var historyId = Guid.NewGuid();
            var fileName = $"{historyId}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var history = new ProjectReportHistory
            {
                Id = historyId,
                ProjectId = projectId,
                ReportName = reportName,
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

        private string FormatFileSize(long bytes)
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
