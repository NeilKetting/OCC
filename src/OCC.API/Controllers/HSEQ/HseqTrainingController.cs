using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Security;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for managing employee HSEQ training records and certificate uploads.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class HseqTrainingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<HseqTrainingController> _logger;

        private static readonly string[] AllowedCertificateExtensions = new[]
        {
            ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx"
        };

        private const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB max file size

        /// <summary>
        /// Initializes a new instance of the <see cref="HseqTrainingController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="env">Web host environment.</param>
        /// <param name="logger">Logger instance.</param>
        public HseqTrainingController(AppDbContext context, IWebHostEnvironment env, ILogger<HseqTrainingController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all HSEQ training records ordered by completion date.
        /// </summary>
        /// <returns>List of training records.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<HseqTrainingRecord>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<HseqTrainingRecord>>> GetTrainingRecords()
        {
            try
            {
                var records = await _context.HseqTrainingRecords
                    .AsNoTracking()
                    .OrderByDescending(t => t.DateCompleted)
                    .ToListAsync();
                return Ok(records);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving training records.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving training records.");
            }
        }

        /// <summary>
        /// Retrieves a specific HSEQ training record by ID.
        /// </summary>
        /// <param name="id">The training record GUID.</param>
        /// <returns>Training record entity if found, or 404 Not Found.</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(HseqTrainingRecord), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<HseqTrainingRecord>> GetTrainingRecord(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid training record ID.");

            try
            {
                var record = await _context.HseqTrainingRecords.FindAsync(id);
                if (record == null) return NotFound();
                return Ok(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving training record {RecordId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving training record.");
            }
        }

        /// <summary>
        /// Retrieves training records that are expiring within the specified number of days.
        /// </summary>
        /// <param name="days">Threshold number of days into the future (0 to 3650).</param>
        /// <returns>List of expiring training records.</returns>
        [HttpGet("expiring/{days}")]
        [ProducesResponseType(typeof(IEnumerable<HseqTrainingRecord>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<HseqTrainingRecord>>> GetExpiringTraining(int days)
        {
            if (days < 0 || days > 3650)
            {
                return BadRequest("Days parameter must be between 0 and 3650.");
            }

            try
            {
                var threshold = DateTime.UtcNow.AddDays(days);
                var today = DateTime.UtcNow;

                var records = await _context.HseqTrainingRecords
                   .AsNoTracking()
                   .Where(t => t.ValidUntil.HasValue && t.ValidUntil <= threshold && t.ValidUntil >= today)
                   .ToListAsync();

                return Ok(records);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expiring training records for threshold {Days} days.", days);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving expiring training records.");
            }
        }

        /// <summary>
        /// Retrieves lightweight HSEQ training summaries for display in dashboards or tables.
        /// </summary>
        /// <returns>List of training summary DTOs.</returns>
        [HttpGet("summaries")]
        [ProducesResponseType(typeof(IEnumerable<HseqTrainingSummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<HseqTrainingSummaryDto>>> GetTrainingSummaries()
        {
            try
            {
                var summaries = await _context.HseqTrainingRecords
                    .AsNoTracking()
                    .OrderByDescending(t => t.DateCompleted)
                    .Select(t => new HseqTrainingSummaryDto
                    {
                        Id = t.Id,
                        EmployeeName = t.EmployeeName,
                        TrainingTopic = t.TrainingTopic,
                        CertificateType = t.CertificateType,
                        DateCompleted = t.DateCompleted,
                        ValidUntil = t.ValidUntil,
                        Role = t.Role,
                        CertificateUrl = t.CertificateUrl,
                        Trainer = t.Trainer
                    })
                    .ToListAsync();

                return Ok(summaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving training summaries.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving training summaries.");
            }
        }

        /// <summary>
        /// Creates a new HSEQ training record.
        /// </summary>
        /// <param name="record">The training record payload.</param>
        /// <returns>The created training record entity.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(typeof(HseqTrainingRecord), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<HseqTrainingRecord>> PostTrainingRecord([FromBody] HseqTrainingRecord record)
        {
            if (record == null) return BadRequest("Training record payload is required.");

            try
            {
                if (record.Id == Guid.Empty)
                {
                    record.Id = Guid.NewGuid();
                }

                record.EmployeeName = InputSanitizer.Sanitize(record.EmployeeName);
                record.TrainingTopic = InputSanitizer.Sanitize(record.TrainingTopic);
                record.CertificateType = InputSanitizer.Sanitize(record.CertificateType);
                record.Role = InputSanitizer.Sanitize(record.Role);
                record.Trainer = InputSanitizer.Sanitize(record.Trainer);
                record.CertificateUrl = InputSanitizer.Sanitize(record.CertificateUrl);

                _context.HseqTrainingRecords.Add(record);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetTrainingRecord), new { id = record.Id }, record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating HSEQ training record.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating training record.");
            }
        }

        /// <summary>
        /// Updates an existing HSEQ training record.
        /// </summary>
        /// <param name="id">The training record GUID matching the payload.</param>
        /// <param name="record">The updated training record entity.</param>
        /// <returns>NoContent on success, or appropriate error response.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PutTrainingRecord(Guid id, [FromBody] HseqTrainingRecord record)
        {
            if (record == null || id == Guid.Empty || id != record.Id)
            {
                return BadRequest("ID mismatch or invalid payload.");
            }

            try
            {
                var existingRecord = await _context.HseqTrainingRecords.FindAsync(id);
                if (existingRecord == null)
                {
                    return NotFound();
                }

                record.EmployeeName = InputSanitizer.Sanitize(record.EmployeeName);
                record.TrainingTopic = InputSanitizer.Sanitize(record.TrainingTopic);
                record.CertificateType = InputSanitizer.Sanitize(record.CertificateType);
                record.Role = InputSanitizer.Sanitize(record.Role);
                record.Trainer = InputSanitizer.Sanitize(record.Trainer);
                record.CertificateUrl = InputSanitizer.Sanitize(record.CertificateUrl);

                _context.Entry(existingRecord).CurrentValues.SetValues(record);

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating training record {RecordId}.", id);
                if (!TrainingRecordExists(id))
                {
                    return NotFound();
                }
                return StatusCode(StatusCodes.Status409Conflict, "A concurrency conflict occurred while updating training record.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating training record {RecordId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating training record.");
            }
        }

        private bool TrainingRecordExists(Guid id)
        {
            return _context.HseqTrainingRecords.Any(e => e.Id == id);
        }

        /// <summary>
        /// Uploads a training certificate file with extension and size checks.
        /// </summary>
        /// <param name="file">The uploaded certificate file.</param>
        /// <returns>Relative URL of the uploaded certificate file.</returns>
        [HttpPost("upload")]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor, SiteManager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<string>> UploadCertificate(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (file.Length > MaxFileSizeBytes)
                return BadRequest($"File size exceeds maximum permitted limit of {MaxFileSizeBytes / (1024 * 1024)}MB.");

            if (!InputSanitizer.IsSafeFileName(file.FileName))
                return BadRequest("File name contains invalid characters or path traversal vectors.");

            var originalName = Path.GetFileName(file.FileName);
            if (!InputSanitizer.IsAllowedExtension(originalName, AllowedCertificateExtensions))
                return BadRequest("File extension is not allowed for certificates.");

            try
            {
                var webRoot = _env.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRoot))
                {
                    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                var uploadsFolder = Path.Combine(webRoot, "uploads", "certificates");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var ext = Path.GetExtension(originalName).ToLowerInvariant();
                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativeUrl = $"/uploads/certificates/{fileName}";
                return Ok(new { Url = relativeUrl }); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading training certificate.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while uploading the certificate.");
            }
        }

        /// <summary>
        /// Deletes an HSEQ training record.
        /// </summary>
        /// <param name="id">The training record GUID.</param>
        /// <returns>NoContent on success, or NotFound.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office, Manager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTrainingRecord(Guid id)
        {
            if (id == Guid.Empty) return BadRequest("Invalid training record ID.");

            try
            {
                var record = await _context.HseqTrainingRecords.FindAsync(id);
                if (record == null) return NotFound();
                
                _context.HseqTrainingRecords.Remove(record);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting training record {RecordId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting training record.");
            }
        }
    }
}

