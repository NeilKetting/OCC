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
    /// API Controller for managing HSEQ safety, compliance, and policy documents.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class HseqDocumentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HseqDocumentsController> _logger;

        private static readonly string[] AllowedDocumentExtensions = new[]
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".csv", ".txt"
        };

        private const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB max file size

        /// <summary>
        /// Initializes a new instance of the <see cref="HseqDocumentsController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="logger">Logger instance.</param>
        public HseqDocumentsController(AppDbContext context, ILogger<HseqDocumentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves HSEQ documents, optionally filtered by project ID (or general documents when null).
        /// </summary>
        /// <param name="projectId">Optional project GUID filter.</param>
        /// <returns>List of HSEQ document entities.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<HseqDocument>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<HseqDocument>>> GetDocuments([FromQuery] Guid? projectId = null)
        {
            try
            {
                var query = _context.HseqDocuments.AsNoTracking().AsQueryable();

                if (projectId.HasValue && projectId.Value != Guid.Empty)
                {
                    query = query.Where(d => d.ProjectId == projectId.Value);
                }
                else if (projectId.HasValue && projectId.Value == Guid.Empty)
                {
                    query = query.Where(d => d.ProjectId == null);
                }

                var docs = await query.OrderByDescending(d => d.UploadDate).ToListAsync();
                return Ok(docs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving HSEQ documents.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving HSEQ documents.");
            }
        }

        /// <summary>
        /// Uploads a new HSEQ document with optional physical file attachment.
        /// </summary>
        /// <param name="document">The document model payload from form.</param>
        /// <param name="file">Optional uploaded file.</param>
        /// <returns>The created HSEQ document record.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(typeof(HseqDocument), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<HseqDocument>> UploadDocument([FromForm] HseqDocument document, IFormFile? file)
        {
            if (document == null) return BadRequest("Document metadata payload is required.");

            try
            {
                document.Id = document.Id != Guid.Empty ? document.Id : Guid.NewGuid();
                document.UploadDate = DateTime.UtcNow;
                document.Title = InputSanitizer.Sanitize(document.Title);
                document.UploadedBy = InputSanitizer.Sanitize(string.IsNullOrWhiteSpace(document.UploadedBy) ? User?.Identity?.Name ?? "Admin" : document.UploadedBy);
                document.Version = InputSanitizer.Sanitize(document.Version);

                if (file != null && file.Length > 0)
                {
                    if (file.Length > MaxFileSizeBytes)
                    {
                        return BadRequest($"File size exceeds maximum permitted limit of {MaxFileSizeBytes / (1024 * 1024)}MB.");
                    }

                    if (!InputSanitizer.IsSafeFileName(file.FileName))
                    {
                        return BadRequest("File name contains invalid characters or path traversal vectors.");
                    }

                    var originalName = Path.GetFileName(file.FileName);
                    if (!InputSanitizer.IsAllowedExtension(originalName, AllowedDocumentExtensions))
                    {
                        return BadRequest("File extension is not allowed for HSEQ documents.");
                    }

                    var categoryFolder = document.Category.ToString();
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "hseq", categoryFolder);
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    var ext = Path.GetExtension(originalName).ToLowerInvariant();
                    var fileName = $"{document.Id}{ext}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    document.FilePath = $"/uploads/hseq/{categoryFolder}/{fileName}";
                    document.FileSize = FormatFileSize(file.Length);
                }

                _context.HseqDocuments.Add(document);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetDocuments), new { id = document.Id }, document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading HSEQ document.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while uploading document.");
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
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Deletes an HSEQ document record and removes its physical file from disk.
        /// </summary>
        /// <param name="id">The document GUID.</param>
        /// <returns>NoContent on success, or NotFound.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office, Manager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid document ID.");

            try
            {
                var document = await _context.HseqDocuments.FindAsync(id);
                if (document == null)
                {
                    return NotFound();
                }

                if (!string.IsNullOrEmpty(document.FilePath))
                {
                    var relative = document.FilePath.TrimStart('/');
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relative);
                    if (System.IO.File.Exists(filePath))
                    {
                        try
                        {
                            System.IO.File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete physical HSEQ document file {FilePath}", filePath);
                        }
                    }
                }

                _context.HseqDocuments.Remove(document);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting HSEQ document {DocumentId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the HSEQ document.");
            }
        }
    }
}

