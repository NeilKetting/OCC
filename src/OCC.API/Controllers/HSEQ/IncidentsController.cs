using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Security;
using OCC.Shared.DTOs;
using OCC.Shared.Enums;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for managing workplace incidents, incident photos, and incident documentation.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class IncidentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<IncidentsController> _logger;

        private static readonly string[] AllowedPhotoExtensions = new[]
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        private static readonly string[] AllowedDocumentExtensions = new[]
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".csv", ".txt"
        };

        private const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB max file size

        /// <summary>
        /// Initializes a new instance of the <see cref="IncidentsController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="logger">Logger instance.</param>
        public IncidentsController(AppDbContext context, ILogger<IncidentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a summary list of all workplace incidents.
        /// </summary>
        /// <returns>List of incident summary DTOs.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<IncidentSummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<IncidentSummaryDto>>> GetIncidents()
        {
            try
            {
                var incidents = await _context.Incidents
                    .Include(i => i.Photos)
                    .Include(i => i.Documents)
                    .AsNoTracking()
                    .OrderByDescending(i => i.Date)
                    .ToListAsync();

                return Ok(incidents.Select(ToSummaryDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving incidents.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving incidents.");
            }
        }

        /// <summary>
        /// Retrieves detailed information for a specific incident by ID.
        /// </summary>
        /// <param name="id">The incident GUID.</param>
        /// <returns>Detailed Incident DTO if found, or 404 Not Found.</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(IncidentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IncidentDto>> GetIncident(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid incident ID.");

            try
            {
                var incident = await _context.Incidents
                    .Include(i => i.Photos)
                    .Include(i => i.Documents)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (incident == null)
                {
                    return NotFound();
                }

                return Ok(ToDetailDto(incident));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving incident {IncidentId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the incident.");
            }
        }

        /// <summary>
        /// Creates a new workplace incident record.
        /// </summary>
        /// <param name="incident">The incident entity payload.</param>
        /// <returns>The created incident detail DTO.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(typeof(IncidentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IncidentDto>> PostIncident([FromBody] Incident incident)
        {
            if (incident == null) return BadRequest("Incident payload is required.");

            try
            {
                if (incident.Id == Guid.Empty)
                {
                    incident.Id = Guid.NewGuid();
                }

                incident.Location = InputSanitizer.Sanitize(incident.Location);
                incident.Description = InputSanitizer.Sanitize(incident.Description);
                incident.ReportedByUserId = InputSanitizer.Sanitize(incident.ReportedByUserId);
                incident.InvestigatorId = InputSanitizer.Sanitize(incident.InvestigatorId);
                incident.RootCause = InputSanitizer.Sanitize(incident.RootCause);
                incident.CorrectiveAction = InputSanitizer.Sanitize(incident.CorrectiveAction);

                if (incident.Date == default)
                {
                    incident.Date = DateTime.UtcNow;
                }

                _context.Incidents.Add(incident);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetIncident), new { id = incident.Id }, ToDetailDto(incident));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating incident.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the incident.");
            }
        }

        /// <summary>
        /// Updates an existing workplace incident.
        /// </summary>
        /// <param name="id">The incident GUID matching the entity.</param>
        /// <param name="incident">The updated incident entity payload.</param>
        /// <returns>NoContent on success, or appropriate error response.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PutIncident(Guid id, [FromBody] Incident incident)
        {
            if (incident == null || id == Guid.Empty || id != incident.Id)
            {
                return BadRequest("Invalid incident ID or payload.");
            }

            try
            {
                var existingIncident = await _context.Incidents.FindAsync(id);
                if (existingIncident == null)
                {
                    return NotFound();
                }

                incident.Location = InputSanitizer.Sanitize(incident.Location);
                incident.Description = InputSanitizer.Sanitize(incident.Description);
                incident.ReportedByUserId = InputSanitizer.Sanitize(incident.ReportedByUserId);
                incident.InvestigatorId = InputSanitizer.Sanitize(incident.InvestigatorId);
                incident.RootCause = InputSanitizer.Sanitize(incident.RootCause);
                incident.CorrectiveAction = InputSanitizer.Sanitize(incident.CorrectiveAction);

                _context.Entry(existingIncident).CurrentValues.SetValues(incident);

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating incident {IncidentId}.", id);
                if (!IncidentExists(id))
                {
                    return NotFound();
                }
                return StatusCode(StatusCodes.Status409Conflict, "A concurrency error occurred while updating the incident.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating incident {IncidentId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the incident.");
            }
        }

        /// <summary>
        /// Soft-deletes a workplace incident record.
        /// </summary>
        /// <param name="id">The incident GUID.</param>
        /// <returns>NoContent on success, or NotFound.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office, Manager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteIncident(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid incident ID.");

            try
            {
                var incident = await _context.Incidents.FindAsync(id);
                if (incident == null)
                {
                    return NotFound();
                }

                _context.Incidents.Remove(incident); // Soft delete handled by context if configured
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting incident {IncidentId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the incident.");
            }
        }

        /// <summary>
        /// Request model for uploading incident photos.
        /// </summary>
        public class IncidentPhotoUploadRequest
        {
            /// <summary> Gets or sets the target Incident GUID. </summary>
            [FromForm] public Guid IncidentId { get; set; }

            /// <summary> Gets or sets the photo file. </summary>
            [FromForm] public IFormFile? File { get; set; }

            /// <summary> Gets or sets optional photo description. </summary>
            [FromForm] public string? Description { get; set; }
        }

        /// <summary>
        /// Uploads an incident photo with extension and size checks.
        /// </summary>
        /// <param name="request">The photo upload request.</param>
        /// <returns>The created photo DTO.</returns>
        [HttpPost("photos")]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(typeof(IncidentPhotoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IncidentPhotoDto>> PostPhoto([FromForm] IncidentPhotoUploadRequest request)
        {
            if (request == null || request.File == null || request.File.Length == 0)
            {
                return BadRequest("No file uploaded or file is empty.");
            }

            if (request.IncidentId == Guid.Empty)
            {
                return BadRequest("Incident ID is required.");
            }

            if (request.File.Length > MaxFileSizeBytes)
            {
                return BadRequest($"File size exceeds maximum permitted limit of {MaxFileSizeBytes / (1024 * 1024)}MB.");
            }

            if (!InputSanitizer.IsSafeFileName(request.File.FileName))
            {
                return BadRequest("File name contains invalid characters or path traversal vectors.");
            }

            var originalName = Path.GetFileName(request.File.FileName);
            if (!InputSanitizer.IsAllowedExtension(originalName, AllowedPhotoExtensions))
            {
                return BadRequest("File extension is not allowed for incident photos.");
            }

            try
            {
                var incident = await _context.Incidents.FindAsync(request.IncidentId);
                if (incident == null) return NotFound("Incident not found.");

                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "incidents");
                if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);

                var ext = Path.GetExtension(originalName).ToLowerInvariant();
                var safeFileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsPath, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                var photo = new IncidentPhoto
                {
                    Id = Guid.NewGuid(),
                    IncidentId = request.IncidentId,
                    FileName = InputSanitizer.Sanitize(originalName),
                    FilePath = $"/uploads/incidents/{safeFileName}",
                    FileSize = $"{(request.File.Length / 1024.0):F2} KB",
                    Description = InputSanitizer.Sanitize(request.Description),
                    UploadedBy = InputSanitizer.Sanitize(User.Identity?.Name ?? "Admin"),
                    UploadedAt = DateTime.UtcNow
                };

                _context.IncidentPhotos.Add(photo);
                await _context.SaveChangesAsync();

                return Ok(ToPhotoDto(photo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading photo for incident {IncidentId}.", request.IncidentId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while uploading incident photo.");
            }
        }

        /// <summary>
        /// Request model for uploading incident documents.
        /// </summary>
        public class IncidentDocumentUploadRequest
        {
            /// <summary> Gets or sets the target Incident GUID. </summary>
            [FromForm] public Guid IncidentId { get; set; }

            /// <summary> Gets or sets the document file. </summary>
            [FromForm] public IFormFile? File { get; set; }
        }

        /// <summary>
        /// Uploads an incident document with extension and size checks.
        /// </summary>
        /// <param name="request">The document upload request.</param>
        /// <returns>The created document DTO.</returns>
        [HttpPost("documents")]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(typeof(IncidentDocumentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IncidentDocumentDto>> PostDocument([FromForm] IncidentDocumentUploadRequest request)
        {
            if (request == null || request.File == null || request.File.Length == 0)
            {
                return BadRequest("No file uploaded or file is empty.");
            }

            if (request.IncidentId == Guid.Empty)
            {
                return BadRequest("Incident ID is required.");
            }

            if (request.File.Length > MaxFileSizeBytes)
            {
                return BadRequest($"File size exceeds maximum permitted limit of {MaxFileSizeBytes / (1024 * 1024)}MB.");
            }

            if (!InputSanitizer.IsSafeFileName(request.File.FileName))
            {
                return BadRequest("File name contains invalid characters or path traversal vectors.");
            }

            var originalName = Path.GetFileName(request.File.FileName);
            if (!InputSanitizer.IsAllowedExtension(originalName, AllowedDocumentExtensions))
            {
                return BadRequest("File extension is not allowed for incident documents.");
            }

            try
            {
                var incident = await _context.Incidents.FindAsync(request.IncidentId);
                if (incident == null) return NotFound("Incident not found.");

                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "incidents");
                if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);

                var ext = Path.GetExtension(originalName).ToLowerInvariant();
                var safeFileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsPath, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                var doc = new IncidentDocument
                {
                    Id = Guid.NewGuid(),
                    IncidentId = request.IncidentId,
                    FileName = InputSanitizer.Sanitize(originalName),
                    FilePath = $"/uploads/incidents/{safeFileName}",
                    FileSize = $"{(request.File.Length / 1024.0):F2} KB",
                    UploadedBy = InputSanitizer.Sanitize(User.Identity?.Name ?? "Admin"),
                    UploadedAt = DateTime.UtcNow
                };

                _context.IncidentDocuments.Add(doc);
                await _context.SaveChangesAsync();

                return Ok(ToDocumentDto(doc));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document for incident {IncidentId}.", request.IncidentId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while uploading incident document.");
            }
        }

        /// <summary>
        /// Deletes an incident document record and removes its physical file from disk.
        /// </summary>
        /// <param name="id">The document GUID.</param>
        /// <returns>NoContent on success, or NotFound.</returns>
        [HttpDelete("documents/{id}")]
        [Authorize(Roles = "Admin, Office, Manager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid document ID.");

            try
            {
                var doc = await _context.IncidentDocuments.FindAsync(id);
                if (doc == null) return NotFound();

                if (!string.IsNullOrEmpty(doc.FilePath))
                {
                    var relative = doc.FilePath.TrimStart('/');
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relative);
                    if (System.IO.File.Exists(filePath))
                    {
                        try
                        {
                            System.IO.File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete physical document file {FilePath}", filePath);
                        }
                    }
                }

                _context.IncidentDocuments.Remove(doc);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting incident document {DocumentId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the document.");
            }
        }

        /// <summary>
        /// Deletes an incident photo record and removes its physical file from disk.
        /// </summary>
        /// <param name="id">The photo GUID.</param>
        /// <returns>NoContent on success, or NotFound.</returns>
        [HttpDelete("photos/{id}")]
        [Authorize(Roles = "Admin, Office, Manager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePhoto(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid photo ID.");

            try
            {
                var photo = await _context.IncidentPhotos.FindAsync(id);
                if (photo == null) return NotFound();

                if (!string.IsNullOrEmpty(photo.FilePath))
                {
                    var relative = photo.FilePath.TrimStart('/');
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relative);
                    if (System.IO.File.Exists(filePath))
                    {
                        try
                        {
                            System.IO.File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete physical photo file {FilePath}", filePath);
                        }
                    }
                }

                _context.IncidentPhotos.Remove(photo);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting incident photo {PhotoId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the photo.");
            }
        }

        private bool IncidentExists(Guid id)
        {
            return _context.Incidents.Any(e => e.Id == id);
        }

        private static IncidentSummaryDto ToSummaryDto(Incident incident)
        {
            return new IncidentSummaryDto
            {
                Id = incident.Id,
                ProjectId = incident.ProjectId,
                Date = incident.Date,
                Type = incident.Type,
                Severity = incident.Severity,
                Location = incident.Location,
                Status = incident.Status,
                ReportedByUserId = incident.ReportedByUserId,
                PhotoCount = incident.Photos?.Count ?? 0,
                DocumentCount = incident.Documents?.Count ?? 0
            };
        }

        private static IncidentDto ToDetailDto(Incident incident)
        {
            return new IncidentDto
            {
                Id = incident.Id,
                ProjectId = incident.ProjectId,
                Date = incident.Date,
                Type = incident.Type,
                Severity = incident.Severity,
                Location = incident.Location,
                Description = incident.Description,
                ReportedByUserId = incident.ReportedByUserId,
                Status = incident.Status,
                InvestigatorId = incident.InvestigatorId,
                RootCause = incident.RootCause,
                CorrectiveAction = incident.CorrectiveAction,
                Photos = incident.Photos?.Select(ToPhotoDto).ToList() ?? new List<IncidentPhotoDto>(),
                Documents = incident.Documents?.Select(ToDocumentDto).ToList() ?? new List<IncidentDocumentDto>()
            };
        }

        private static IncidentPhotoDto ToPhotoDto(IncidentPhoto photo)
        {
            return new IncidentPhotoDto
            {
                Id = photo.Id,
                FileName = photo.FileName,
                FilePath = photo.FilePath,
                FileSize = photo.FileSize,
                UploadedBy = photo.UploadedBy,
                UploadedAt = photo.UploadedAt
            };
        }

        private static IncidentDocumentDto ToDocumentDto(IncidentDocument doc)
        {
            return new IncidentDocumentDto
            {
                Id = doc.Id,
                FileName = doc.FileName,
                FilePath = doc.FilePath,
                FileSize = doc.FileSize,
                UploadedBy = doc.UploadedBy,
                UploadedAt = doc.UploadedAt
            };
        }
    }
}

