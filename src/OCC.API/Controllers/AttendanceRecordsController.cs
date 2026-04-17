using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AttendanceRecordsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<AttendanceRecordsController> _logger;

        public AttendanceRecordsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<AttendanceRecordsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/AttendanceRecords
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AttendanceRecord>>> GetAttendanceRecords()
        {
            try
            {
                return await _context.AttendanceRecords.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance records");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/AttendanceRecords/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AttendanceRecord>> GetAttendanceRecord(Guid id)
        {
            try
            {
                var record = await _context.AttendanceRecords.FindAsync(id);
                if (record == null) return NotFound();
                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance record {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/AttendanceRecords
        [HttpPost]
        public async Task<ActionResult<AttendanceRecord>> PostAttendanceRecord(AttendanceRecord record)
        {
            var errorResponse = ValidateAttendanceRecord(record);
            if (errorResponse != null)
                return BadRequest(errorResponse);

            try
            {
                if (record.Id == Guid.Empty) record.Id = Guid.NewGuid();
                
                // Calculate hours before saving
                CalculateHoursWorked(record);

                _context.AttendanceRecords.Add(record);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Create", record.Id);
                
                return CreatedAtAction("GetAttendanceRecord", new { id = record.Id }, record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attendance record");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/AttendanceRecords/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAttendanceRecord(Guid id, AttendanceRecord record)
        {
            if (id != record.Id) return BadRequest();

            var errorResponse = ValidateAttendanceRecord(record);
            if (errorResponse != null)
                return BadRequest(errorResponse);

            // Calculate hours before saving
            CalculateHoursWorked(record);

            _context.Entry(record).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AttendanceRecordExists(id)) return NotFound();
                else throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attendance record {Id}", id);
                return StatusCode(500, "Internal server error");
            }
            return NoContent();
        }

        // DELETE: api/AttendanceRecords/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAttendanceRecord(Guid id)
        {
            try
            {
                var record = await _context.AttendanceRecords.FindAsync(id);
                if (record == null) return NotFound();
                _context.AttendanceRecords.Remove(record);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attendance record {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private bool AttendanceRecordExists(Guid id) => _context.AttendanceRecords.Any(e => e.Id == id);

        [HttpPost("upload")]
        public async Task<ActionResult<string>> UploadNote(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "notes");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative path
            return Ok($"/uploads/notes/{uniqueFileName}");
        }

        private void CalculateHoursWorked(AttendanceRecord record)
        {
            if (record.CheckInTime != null && record.CheckOutTime != null)
            {
                var duration = record.CheckOutTime.Value - record.CheckInTime.Value;
                if (duration.TotalHours > 0)
                {
                    // Subtract lunch (standard 45 mins = 0.75 hours) if worked more than 5 hours
                    double lunchHours = 0;
                    if (duration.TotalHours > 5)
                    {
                        lunchHours = 0.75;
                    }
                    record.HoursWorked = Math.Max(0, Math.Round(duration.TotalHours - lunchHours, 2));
                }
                else
                {
                    record.HoursWorked = 0;
                }
            }
            else
            {
                record.HoursWorked = 0;
            }
        }

        private string? ValidateAttendanceRecord(AttendanceRecord record)
        {
            var now = DateTime.Now;

            // 1. Future time checks (Allow 1 minute leniency for server-client desync)
            if (record.CheckInTime.HasValue && record.CheckInTime.Value > now.AddMinutes(1))
                return "Clock-in time cannot be in the future.";
            
            if (record.CheckOutTime.HasValue && record.CheckOutTime.Value > now.AddMinutes(1))
                return "Clock-out time cannot be in the future.";

            // 2. Order check
            if (record.CheckInTime.HasValue && record.CheckOutTime.HasValue)
            {
                if (record.CheckOutTime.Value < record.CheckInTime.Value)
                    return "Clock-out time cannot be before clock-in time.";
            }

            // 3. Overlap check for the same employee
            var overlappingRecords = _context.AttendanceRecords
                .Where(r => r.EmployeeId == record.EmployeeId && r.Id != record.Id && r.Date.Date == record.Date.Date)
                .ToList();

            foreach (var other in overlappingRecords)
            {
                // 3a. Check for multiple open shifts
                if (record.CheckOutTime == null && other.CheckOutTime == null)
                    return "Employee already has an open shift.";

                // 3b. Temporal overlap
                DateTime thisIn = record.CheckInTime ?? record.Date.Date;
                DateTime thisOut = record.CheckOutTime ?? DateTime.MaxValue;

                DateTime otherIn = other.CheckInTime ?? other.Date.Date;
                DateTime otherOut = other.CheckOutTime ?? DateTime.MaxValue;

                // Simple overlap condition
                if (thisIn < otherOut && thisOut > otherIn)
                {
                    return $"Shift overlaps with another recorded shift (In: {otherIn:HH:mm}, Out: {(other.CheckOutTime.HasValue ? otherOut.ToString("HH:mm") : "Open")}).";
                }
            }

            return null;
        }
    }
}
