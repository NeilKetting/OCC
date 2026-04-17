using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HseqTrainingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public HseqTrainingController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<HseqTrainingRecord>>> GetTrainingRecords()
        {
            return await _context.HseqTrainingRecords
                .OrderByDescending(t => t.DateCompleted)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<HseqTrainingRecord>> GetTrainingRecord(Guid id)
        {
            var record = await _context.HseqTrainingRecords.FindAsync(id);
            if (record == null) return NotFound();
            return record;
        }
        
        [HttpGet("expiring/{days}")]
        public async Task<ActionResult<IEnumerable<HseqTrainingRecord>>> GetExpiringTraining(int days)
        {
             var threshold = DateTime.UtcNow.AddDays(days);
             var today = DateTime.UtcNow;
             
             return await _context.HseqTrainingRecords
                .Where(t => t.ValidUntil.HasValue && t.ValidUntil <= threshold && t.ValidUntil >= today)
                .ToListAsync();
        }

        [HttpGet("summaries")]
        public async Task<ActionResult<IEnumerable<OCC.Shared.DTOs.HseqTrainingSummaryDto>>> GetTrainingSummaries()
        {
            return await _context.HseqTrainingRecords
                .OrderByDescending(t => t.DateCompleted)
                .Select(t => new OCC.Shared.DTOs.HseqTrainingSummaryDto
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
        }

        [HttpPost]
        public async Task<ActionResult<HseqTrainingRecord>> PostTrainingRecord(HseqTrainingRecord record)
        {
            _context.HseqTrainingRecords.Add(record);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetTrainingRecords", new { id = record.Id }, record);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTrainingRecord(Guid id, HseqTrainingRecord record)
        {
            if (id != record.Id)
            {
                return BadRequest("ID mismatch");
            }

            _context.Entry(record).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TrainingRecordExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        private bool TrainingRecordExists(Guid id)
        {
            return _context.HseqTrainingRecords.Any(e => e.Id == id);
        }

        [HttpPost("upload")]
        public async Task<ActionResult<string>> UploadCertificate(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            try
            {
                var webRoot = _env.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRoot))
                {
                    // Fallback for some local dev setups if WebRootPath is null (unlikely with UseStaticFiles but safe)
                    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                var uploadsFolder = Path.Combine(webRoot, "uploads", "certificates");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                // Unique filename
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return server-relative URL
                var relativeUrl = $"/uploads/certificates/{fileName}";
                return Ok(new { Url = relativeUrl }); 
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTrainingRecord(Guid id)
        {
            var record = await _context.HseqTrainingRecords.FindAsync(id);
            if (record == null) return NotFound();
            
            _context.HseqTrainingRecords.Remove(record);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
