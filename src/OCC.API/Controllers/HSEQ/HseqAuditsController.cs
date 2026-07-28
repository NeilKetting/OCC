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
    /// API Controller for managing HSEQ (Health, Safety, Environment, Quality) Audits, sections, non-compliance items, and audit attachments.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class HseqAuditsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HseqAuditsController> _logger;

        private static readonly string[] AllowedAttachmentExtensions = new[]
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".csv", ".txt"
        };

        private const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB max file size

        /// <summary>
        /// Initializes a new instance of the <see cref="HseqAuditsController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="logger">Logger instance.</param>
        public HseqAuditsController(AppDbContext context, ILogger<HseqAuditsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all HSEQ audit summaries, optionally filtered by project ID.
        /// </summary>
        /// <param name="projectId">Optional project GUID filter.</param>
        /// <returns>List of audit summary DTOs.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<AuditSummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<AuditSummaryDto>>> GetAudits([FromQuery] Guid? projectId = null)
        {
            try
            {
                var query = _context.HseqAudits.AsNoTracking();
                if (projectId.HasValue && projectId.Value != Guid.Empty)
                {
                    var project = await _context.Projects.FindAsync(projectId.Value);
                    if (project != null)
                    {
                        var projName = project.Name;
                        query = query.Where(a => a.ProjectId == projectId.Value || (a.SiteName != null && a.SiteName.Contains(projName)));
                    }
                    else
                    {
                        query = query.Where(a => a.ProjectId == projectId.Value);
                    }
                }

                var audits = await query
                    .OrderByDescending(a => a.Date)
                    .ToListAsync();

                return Ok(audits.Select(ToSummaryDto).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving HSEQ audit list.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving audits.");
            }
        }

        /// <summary>
        /// Retrieves detailed information for a specific HSEQ audit by ID.
        /// </summary>
        /// <param name="id">The audit GUID.</param>
        /// <returns>Detailed Audit DTO if found, or 404 Not Found.</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(AuditDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AuditDto>> GetAudit(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid audit ID.");

            try
            {
                var audit = await _context.HseqAudits
                    .Include(a => a.Sections)
                    .Include(a => a.ComplianceItems)
                    .Include(a => a.NonComplianceItems)
                        .ThenInclude(i => i.Attachments)
                    .Include(a => a.Attachments)
                    .AsNoTracking()
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (audit == null)
                {
                    return NotFound();
                }

                return Ok(ToDetailDto(audit));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit {AuditId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the audit.");
            }
        }

        /// <summary>
        /// Creates a new HSEQ audit record with optional sections and non-compliance items.
        /// </summary>
        /// <param name="auditDto">The audit creation DTO.</param>
        /// <returns>The created audit DTO.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(typeof(AuditDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuditDto>> PostAudit([FromBody] AuditDto auditDto)
        {
            if (auditDto == null) return BadRequest("Audit data is required.");

            try
            {
                var audit = new HseqAudit
                {
                    Id = auditDto.Id != Guid.Empty ? auditDto.Id : Guid.NewGuid(),
                    ProjectId = auditDto.ProjectId,
                    Date = auditDto.Date == default ? DateTime.UtcNow : auditDto.Date,
                    SiteName = InputSanitizer.Sanitize(auditDto.SiteName),
                    ScopeOfWorks = InputSanitizer.Sanitize(auditDto.ScopeOfWorks),
                    SiteManager = InputSanitizer.Sanitize(auditDto.SiteManager),
                    SiteSupervisor = InputSanitizer.Sanitize(auditDto.SiteSupervisor),
                    HseqConsultant = InputSanitizer.Sanitize(auditDto.HseqConsultant),
                    AuditNumber = InputSanitizer.Sanitize(auditDto.AuditNumber),
                    TargetScore = auditDto.TargetScore,
                    ActualScore = auditDto.ActualScore,
                    Status = auditDto.Status,
                    CloseOutDate = auditDto.CloseOutDate,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                // Map Sections
                if (auditDto.Sections != null)
                {
                    foreach (var s in auditDto.Sections)
                    {
                        audit.Sections.Add(new HseqAuditSection
                        {
                            Id = s.Id != Guid.Empty ? s.Id : Guid.NewGuid(),
                            Name = InputSanitizer.Sanitize(s.Name),
                            PossibleScore = s.PossibleScore,
                            ActualScore = s.ActualScore
                        });
                    }
                }

                // Map NonComplianceItems
                if (auditDto.NonComplianceItems != null)
                {
                    foreach (var i in auditDto.NonComplianceItems)
                    {
                        audit.NonComplianceItems.Add(new HseqAuditNonComplianceItem
                        {
                            Id = i.Id != Guid.Empty ? i.Id : Guid.NewGuid(),
                            Description = InputSanitizer.Sanitize(i.Description),
                            RegulationReference = InputSanitizer.Sanitize(i.RegulationReference),
                            CorrectiveAction = InputSanitizer.Sanitize(i.CorrectiveAction),
                            ResponsiblePerson = InputSanitizer.Sanitize(i.ResponsiblePerson),
                            TargetDate = i.TargetDate,
                            Status = i.Status,
                            ClosedDate = i.ClosedDate,
                            CreatedAtUtc = DateTime.UtcNow,
                            UpdatedAtUtc = DateTime.UtcNow
                        });
                    }
                }

                _context.HseqAudits.Add(audit);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetAudit), new { id = audit.Id }, ToDetailDto(audit));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating HSEQ audit.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the audit.");
            }
        }

        /// <summary>
        /// Updates an existing HSEQ audit record and its associated sections and non-compliance items.
        /// </summary>
        /// <param name="id">The audit GUID matching the DTO.</param>
        /// <param name="auditDto">The updated audit DTO.</param>
        /// <returns>NoContent on success, or appropriate error response.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PutAudit(Guid id, [FromBody] AuditDto auditDto)
        {
            if (auditDto == null || id == Guid.Empty || id != auditDto.Id)
            {
                return BadRequest("Invalid audit ID or payload.");
            }

            try
            {
                var existingAudit = await _context.HseqAudits
                    .Include(a => a.Sections)
                    .Include(a => a.NonComplianceItems)
                        .ThenInclude(i => i.Attachments)
                    .Include(a => a.Attachments)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (existingAudit == null)
                {
                    return NotFound();
                }

                existingAudit.SiteName = InputSanitizer.Sanitize(string.IsNullOrWhiteSpace(auditDto.SiteName) ? existingAudit.SiteName : auditDto.SiteName);
                existingAudit.AuditNumber = InputSanitizer.Sanitize(string.IsNullOrWhiteSpace(auditDto.AuditNumber) ? existingAudit.AuditNumber : auditDto.AuditNumber);
                if (!string.IsNullOrWhiteSpace(auditDto.HseqConsultant)) existingAudit.HseqConsultant = InputSanitizer.Sanitize(auditDto.HseqConsultant);
                if (!string.IsNullOrWhiteSpace(auditDto.SiteManager)) existingAudit.SiteManager = InputSanitizer.Sanitize(auditDto.SiteManager);
                if (!string.IsNullOrWhiteSpace(auditDto.SiteSupervisor)) existingAudit.SiteSupervisor = InputSanitizer.Sanitize(auditDto.SiteSupervisor);
                if (!string.IsNullOrWhiteSpace(auditDto.ScopeOfWorks)) existingAudit.ScopeOfWorks = InputSanitizer.Sanitize(auditDto.ScopeOfWorks);
                if (auditDto.TargetScore > 0) existingAudit.TargetScore = auditDto.TargetScore;
                if (auditDto.ActualScore > 0) existingAudit.ActualScore = auditDto.ActualScore;
                existingAudit.UpdatedAtUtc = DateTime.UtcNow;

                // Update Sections
                if (auditDto.Sections != null)
                {
                    var sectionIdsInDto = auditDto.Sections.Where(s => s.Id != Guid.Empty).Select(s => s.Id).ToList();
                    var sectionsToRemove = existingAudit.Sections.Where(s => !sectionIdsInDto.Contains(s.Id)).ToList();
                    foreach (var s in sectionsToRemove) existingAudit.Sections.Remove(s);

                    foreach (var sectionDto in auditDto.Sections)
                    {
                        var existingSection = existingAudit.Sections.FirstOrDefault(s => s.Id == sectionDto.Id);
                        if (existingSection != null)
                        {
                            existingSection.ActualScore = sectionDto.ActualScore;
                            existingSection.PossibleScore = sectionDto.PossibleScore;
                            existingSection.Name = InputSanitizer.Sanitize(sectionDto.Name);
                            existingSection.UpdatedAtUtc = DateTime.UtcNow;
                        }
                        else
                        {
                            existingAudit.Sections.Add(new HseqAuditSection
                            {
                                Id = sectionDto.Id != Guid.Empty ? sectionDto.Id : Guid.NewGuid(),
                                Name = InputSanitizer.Sanitize(sectionDto.Name),
                                PossibleScore = sectionDto.PossibleScore,
                                ActualScore = sectionDto.ActualScore,
                                AuditId = existingAudit.Id,
                                CreatedAtUtc = DateTime.UtcNow,
                                UpdatedAtUtc = DateTime.UtcNow
                            });
                        }
                    }
                }

                // Update NonComplianceItems
                if (auditDto.NonComplianceItems != null)
                {
                    var itemIdsInDto = auditDto.NonComplianceItems.Where(i => i.Id != Guid.Empty).Select(i => i.Id).ToList();
                    var itemsToRemove = existingAudit.NonComplianceItems.Where(i => !itemIdsInDto.Contains(i.Id)).ToList();
                    foreach (var i in itemsToRemove) existingAudit.NonComplianceItems.Remove(i);

                    foreach (var itemDto in auditDto.NonComplianceItems)
                    {
                        var existingItem = existingAudit.NonComplianceItems.FirstOrDefault(i => i.Id == itemDto.Id);
                        if (existingItem != null)
                        {
                            existingItem.Description = InputSanitizer.Sanitize(itemDto.Description);
                            existingItem.RegulationReference = InputSanitizer.Sanitize(itemDto.RegulationReference);
                            existingItem.CorrectiveAction = InputSanitizer.Sanitize(itemDto.CorrectiveAction);
                            existingItem.ResponsiblePerson = InputSanitizer.Sanitize(itemDto.ResponsiblePerson);
                            existingItem.TargetDate = itemDto.TargetDate;
                            existingItem.Status = itemDto.Status;
                            existingItem.ClosedDate = itemDto.ClosedDate;
                            existingItem.UpdatedAtUtc = DateTime.UtcNow;
                        }
                        else
                        {
                            existingAudit.NonComplianceItems.Add(new HseqAuditNonComplianceItem
                            {
                                Id = itemDto.Id != Guid.Empty ? itemDto.Id : Guid.NewGuid(),
                                Description = InputSanitizer.Sanitize(itemDto.Description),
                                RegulationReference = InputSanitizer.Sanitize(itemDto.RegulationReference),
                                CorrectiveAction = InputSanitizer.Sanitize(itemDto.CorrectiveAction),
                                ResponsiblePerson = InputSanitizer.Sanitize(itemDto.ResponsiblePerson),
                                TargetDate = itemDto.TargetDate,
                                Status = itemDto.Status,
                                ClosedDate = itemDto.ClosedDate,
                                CreatedAtUtc = DateTime.UtcNow,
                                UpdatedAtUtc = DateTime.UtcNow,
                                AuditId = existingAudit.Id
                            });
                        }
                    }
                }
                else
                {
                    existingAudit.NonComplianceItems.Clear();
                }

                // Save child changes first
                var retryCount = 0;
                const int MaxRetries = 3;
                while (retryCount < MaxRetries)
                {
                    try
                    {
                        await _context.SaveChangesAsync();
                        break;
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        retryCount++;
                        _logger.LogWarning(ex, "Concurrency conflict detected updating children. Retry {Retry}/{Max}.", retryCount, MaxRetries);

                        foreach (var entry in ex.Entries)
                        {
                            var dbValues = await entry.GetDatabaseValuesAsync();
                            if (dbValues == null)
                            {
                                entry.State = EntityState.Added;
                            }
                            else
                            {
                                var dbRowVersion = dbValues["RowVersion"];
                                entry.Property("RowVersion").OriginalValue = dbRowVersion;
                            }
                        }

                        if (retryCount >= MaxRetries) throw;
                    }
                }

                // Update Parent Properties
                if (_context.Database.IsRelational())
                {
                    await _context.Entry(existingAudit).ReloadAsync();
                }

                existingAudit.ProjectId = auditDto.ProjectId;
                existingAudit.Date = auditDto.Date;
                existingAudit.SiteName = InputSanitizer.Sanitize(auditDto.SiteName);
                existingAudit.SiteManager = InputSanitizer.Sanitize(auditDto.SiteManager);
                existingAudit.SiteSupervisor = InputSanitizer.Sanitize(auditDto.SiteSupervisor);
                existingAudit.HseqConsultant = InputSanitizer.Sanitize(auditDto.HseqConsultant);
                existingAudit.ScopeOfWorks = InputSanitizer.Sanitize(auditDto.ScopeOfWorks);
                existingAudit.Status = auditDto.Status;
                existingAudit.TargetScore = auditDto.TargetScore;
                existingAudit.ActualScore = auditDto.ActualScore;
                existingAudit.CloseOutDate = auditDto.CloseOutDate;
                existingAudit.UpdatedAtUtc = DateTime.UtcNow;

                retryCount = 0;
                while (retryCount < MaxRetries)
                {
                    try
                    {
                        await _context.SaveChangesAsync();
                        break;
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        retryCount++;
                        _logger.LogWarning(ex, "Concurrency conflict detected updating parent Audit. Retry {Retry}/{Max}.", retryCount, MaxRetries);

                        foreach (var entry in ex.Entries)
                        {
                            var dbValues = await entry.GetDatabaseValuesAsync();
                            if (dbValues == null)
                            {
                                return NotFound();
                            }
                            else
                            {
                                var dbRowVersion = dbValues["RowVersion"];
                                entry.Property("RowVersion").OriginalValue = dbRowVersion;
                            }
                        }
                        if (retryCount >= MaxRetries) throw;
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating audit {AuditId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the audit.");
            }
        }

        /// <summary>
        /// Retrieves all non-compliance items (deviations) recorded for a given audit ID.
        /// </summary>
        /// <param name="id">The audit GUID.</param>
        /// <returns>List of non-compliance item DTOs.</returns>
        [HttpGet("{id}/deviations")]
        [ProducesResponseType(typeof(IEnumerable<AuditNonComplianceItemDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<AuditNonComplianceItemDto>>> GetAuditDeviations(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid audit ID.");

            try
            {
                var items = await _context.HseqAuditNonComplianceItems
                   .Include(i => i.Attachments)
                   .AsNoTracking()
                   .Where(i => i.AuditId == id)
                   .ToListAsync();

                return Ok(items.Select(ToNonComplianceItemDto).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving deviations for audit {AuditId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving deviations.");
            }
        }

        /// <summary>
        /// Request model for uploading audit attachments.
        /// </summary>
        public class HseqAuditAttachmentRequest
        {
            /// <summary> Gets or sets the target Audit GUID. </summary>
            [FromForm] public Guid AuditId { get; set; }

            /// <summary> Gets or sets the optional target Non-Compliance Item GUID. </summary>
            [FromForm] public Guid? NonComplianceItemId { get; set; }

            /// <summary> Gets or sets the uploaded file. </summary>
            [FromForm] public IFormFile? File { get; set; }
        }

        /// <summary>
        /// Uploads an attachment for an audit or non-compliance item with extension and size checks.
        /// </summary>
        /// <param name="request">The file attachment request.</param>
        /// <returns>The created attachment record.</returns>
        [HttpPost("attachments")]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(typeof(HseqAuditAttachment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<HseqAuditAttachment>> PostAttachment([FromForm] HseqAuditAttachmentRequest request)
        {
            if (request == null || request.File == null || request.File.Length == 0)
            {
                return BadRequest("No file uploaded or file is empty.");
            }

            if (request.AuditId == Guid.Empty)
            {
                return BadRequest("Audit ID is required.");
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
            if (!InputSanitizer.IsAllowedExtension(originalName, AllowedAttachmentExtensions))
            {
                return BadRequest("File extension is not allowed.");
            }

            try
            {
                var audit = await _context.HseqAudits.FindAsync(request.AuditId);
                if (audit == null) return NotFound("Audit not found.");

                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audits");
                if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);

                var ext = Path.GetExtension(originalName).ToLowerInvariant();
                var safeFileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsPath, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                var attachment = new HseqAuditAttachment
                {
                    Id = Guid.NewGuid(),
                    AuditId = request.AuditId,
                    NonComplianceItemId = request.NonComplianceItemId,
                    FileName = InputSanitizer.Sanitize(originalName),
                    FilePath = $"/uploads/audits/{safeFileName}",
                    FileSize = $"{(request.File.Length / 1024.0):F2} KB",
                    UploadedBy = InputSanitizer.Sanitize(User.Identity?.Name ?? "Admin"),
                    UploadedAt = DateTime.UtcNow
                };

                _context.HseqAuditAttachments.Add(attachment);
                await _context.SaveChangesAsync();

                return Ok(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading attachment for audit {AuditId}.", request.AuditId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while uploading attachment.");
            }
        }

        /// <summary>
        /// Deletes an HSEQ audit and removes physical files associated with its attachments.
        /// </summary>
        /// <param name="id">The audit GUID.</param>
        /// <returns>NoContent on success, or NotFound.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office, Manager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAudit(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid audit ID.");

            try
            {
                var audit = await _context.HseqAudits.FindAsync(id);
                if (audit == null) return NotFound();

                var attachments = await _context.HseqAuditAttachments.Where(a => a.AuditId == id).ToListAsync();
                foreach (var attachment in attachments)
                {
                    if (!string.IsNullOrEmpty(attachment.FilePath))
                    {
                        var relative = attachment.FilePath.TrimStart('/');
                        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relative);
                        if (System.IO.File.Exists(filePath))
                        {
                            try
                            {
                                System.IO.File.Delete(filePath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to delete physical file {FilePath}", filePath);
                            }
                        }
                    }
                }

                _context.HseqAudits.Remove(audit);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting audit {AuditId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the audit.");
            }
        }

        /// <summary>
        /// Deletes a specific audit attachment and removes its physical file from disk.
        /// </summary>
        /// <param name="id">The attachment GUID.</param>
        /// <returns>NoContent on success, or NotFound.</returns>
        [HttpDelete("attachments/{id}")]
        [Authorize(Roles = "Admin, Office, Manager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAttachment(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid attachment ID.");

            try
            {
                var attachment = await _context.HseqAuditAttachments.FindAsync(id);
                if (attachment == null) return NotFound();

                if (!string.IsNullOrEmpty(attachment.FilePath))
                {
                    var relative = attachment.FilePath.TrimStart('/');
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relative);
                    if (System.IO.File.Exists(filePath))
                    {
                        try
                        {
                            System.IO.File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete physical attachment file {FilePath}", filePath);
                        }
                    }
                }

                _context.HseqAuditAttachments.Remove(attachment);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attachment {AttachmentId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting attachment.");
            }
        }

        #region Mapping Helpers

        private static AuditSummaryDto ToSummaryDto(HseqAudit audit)
        {
            return new AuditSummaryDto
            {
                Id = audit.Id,
                ProjectId = audit.ProjectId,
                Date = audit.Date,
                SiteName = audit.SiteName,
                AuditNumber = audit.AuditNumber,
                Status = audit.Status,
                HseqConsultant = audit.HseqConsultant,
                TargetScore = audit.TargetScore,
                ActualScore = audit.ActualScore
            };
        }

        private static AuditDto ToDetailDto(HseqAudit audit)
        {
            return new AuditDto
            {
                Id = audit.Id,
                ProjectId = audit.ProjectId,
                Date = audit.Date,
                SiteName = audit.SiteName,
                ScopeOfWorks = audit.ScopeOfWorks,
                SiteManager = audit.SiteManager,
                SiteSupervisor = audit.SiteSupervisor,
                HseqConsultant = audit.HseqConsultant,
                AuditNumber = audit.AuditNumber,
                TargetScore = audit.TargetScore,
                ActualScore = audit.ActualScore,
                Status = audit.Status,
                CloseOutDate = audit.CloseOutDate,
                Sections = audit.Sections.Select(s => new AuditSectionDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    PossibleScore = s.PossibleScore,
                    ActualScore = s.ActualScore,
                    RowVersion = s.RowVersion ?? Array.Empty<byte>()
                }).ToList(),
                NonComplianceItems = audit.NonComplianceItems.Select(ToNonComplianceItemDto).ToList(),
                Attachments = audit.Attachments.Select(ToAttachmentDto).ToList(),
                RowVersion = audit.RowVersion ?? Array.Empty<byte>()
            };
        }

        private static AuditNonComplianceItemDto ToNonComplianceItemDto(HseqAuditNonComplianceItem item)
        {
            return new AuditNonComplianceItemDto
            {
                Id = item.Id,
                Description = item.Description,
                RegulationReference = item.RegulationReference,
                CorrectiveAction = item.CorrectiveAction,
                ResponsiblePerson = item.ResponsiblePerson,
                TargetDate = item.TargetDate,
                Status = item.Status,
                ClosedDate = item.ClosedDate,
                Attachments = item.Attachments?.Select(ToAttachmentDto).ToList() ?? new List<AuditAttachmentDto>(),
                RowVersion = item.RowVersion ?? Array.Empty<byte>()
            };
        }

        private static AuditAttachmentDto ToAttachmentDto(HseqAuditAttachment attachment)
        {
            return new AuditAttachmentDto
            {
                Id = attachment.Id,
                NonComplianceItemId = attachment.NonComplianceItemId,
                FileName = attachment.FileName,
                FilePath = attachment.FilePath,
                FileSize = attachment.FileSize,
                UploadedBy = attachment.UploadedBy,
                UploadedAt = attachment.UploadedAt
            };
        }

        #endregion
    }
}

